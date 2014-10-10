/// Contains connectors for F# Agents to Azure Service Bus Queues as either Actors or Agents.
module FSharp.CloudAgent.ConnectionFactory

open FSharp.CloudAgent
open FSharp.CloudAgent.Actors
open FSharp.CloudAgent.Messaging
open System

[<AutoOpen>]
module internal Helpers =
    open System.Threading
    let createDisposable() =
        let source = new CancellationTokenSource()
        let disposable =
            { new IDisposable with
                member __.Dispose() = source.Cancel() }
        disposable, source.Token        

module internal Actors =
    open System.Threading

    /// Contains configuration details for connecting to an Actor Cloud.
    type ActorCloudOptions<'a> = 
        {   PollTime : TimeSpan
            Serializer : ISerializer<'a>
            ActorStore : IActorStore<'a> }

    /// Binds a Cloud Agent to a session-enabled Azure Service Bus Queue with affinity between
    /// sessions and agents, using an IActorStore to manage actor lifetime.
    let BindToCloud<'a> (getNextSession, options:ActorCloudOptions<'a>) = 
        let processBrokeredMessage = ProcessBrokeredMessage options.Serializer
        
        /// Locks into a loop, processing messages from a specific session with a specific agent until the session is empty.
        let processSession (agent : CloudAgentKind<'a>, session : IActorMessageStream, cancellationToken:CancellationToken) = 
            let processBrokeredMessage = processBrokeredMessage agent
            let rec continueProcessingStream() =
                async {
                    let! message = session.GetNextMessage() |> Async.CatchException
                    match cancellationToken.IsCancellationRequested, message with
                    | true, _
                    | _, Error _
                    | _, Result None -> options.ActorStore.RemoveActor(session.SessionId)
                    | _, Result(Some message) -> 
                        let! result = processBrokeredMessage message
                        do! match result with
                            | Completed -> session.CompleteMessage(message.LockToken)
                            | Failed -> session.AbandonMessage(message.LockToken)
                            | Abandoned -> session.DeadLetterMessage(message.LockToken)
                        let! renewal = session.RenewSessionLock() |> Async.CatchException
                        match renewal with
                        | Result() -> return! continueProcessingStream()
                        | Error _ -> options.ActorStore.RemoveActor(session.SessionId)
                }
            continueProcessingStream()

        let disposable, cancellationToken = createDisposable()
        async { 
            let getNextSession = getNextSession |> withAutomaticRetry
            while not cancellationToken.IsCancellationRequested do
                let! (session : IActorMessageStream) = getNextSession (options.PollTime.TotalMilliseconds |> int)
                match cancellationToken.IsCancellationRequested with
                | true -> ()
                | false ->
                    let agent = options.ActorStore.GetActor(session.SessionId)
                    processSession (agent, session, cancellationToken) |> Async.Start
        }
        |> Async.Start
        disposable
module internal Workers =
    open System
    open FSharp.CloudAgent.Messaging
    open FSharp.CloudAgent.Actors.Factory

    type WorkerCloudOptions<'a> = 
        {   PollTime : TimeSpan
            Serializer : ISerializer<'a>
            GetNextAgent : AgentSelectorFunc<'a> }

    /// Binds CloudAgents to an Azure service bus queue using custom configuration options.
    let BindToCloud<'a>(messageStream:ICloudMessageStream, options : WorkerCloudOptions<'a>) = 
        let getNextMessage = messageStream.GetNextMessage |> withAutomaticRetry
        let processBrokeredMessage = ProcessBrokeredMessage options.Serializer
        let disposable, token = createDisposable()
        async { 
            while not token.IsCancellationRequested do
                // Only try to get a message once we have an available agent.
                let! agent = options.GetNextAgent()
                let! message = getNextMessage (options.PollTime.TotalMilliseconds |> int)
                match token.IsCancellationRequested with
                | true ->
                    // If a cancellation request has occurred, don't process the last received message
                    // - just throw it back to the message provider.
                    messageStream.AbandonMessage message.LockToken |> ignore
                | false ->
                    // Place the following code in its own async block so it works in the background and we can
                    // get another message for another agent in parallel.
                    async {
                        let! processingResult = processBrokeredMessage agent message
                        match processingResult with
                        | Completed -> do! messageStream.CompleteMessage(message.LockToken)
                        | Failed -> do! messageStream.AbandonMessage(message.LockToken)
                        | Abandoned -> do! messageStream.DeadLetterMessage(message.LockToken)
                    }
                    |> Async.Start
        }
        |> Async.Start        
        disposable

open Workers
open Actors
open FSharp.CloudAgent.Connections
open FSharp.CloudAgent.Actors.Factory

/// <summary>
/// Starts listening to Azure for messages that are handled by agents.
/// </summary>
/// <param name="cloudConnection">The connection to the Azure service bus that will provide messages.</param>
/// <param name="createAgentFunc">A function that can create a single F# Agent to handle messages.</param>
let StartListening<'a>(cloudConnection, createAgentFunc) =
    match cloudConnection with
    | WorkerCloudConnection (ConnectionString connection, Queue queue) ->
        let options = { PollTime = TimeSpan.FromSeconds 10.; Serializer = JsonSerializer<'a>(); GetNextAgent = CreateAgentSelector(512, createAgentFunc) }
        let messageStream = CreateQueueStream(connection, queue)
        Workers.BindToCloud(messageStream, options)
    | ActorCloudConnection (ConnectionString connection, Queue queue) ->
        let options = { PollTime = TimeSpan.FromSeconds 10.; Serializer = JsonSerializer<'a>(); ActorStore = CreateActorStore(createAgentFunc) }
        let getNextSessionStream = CreateActorMessageStream(connection, queue)
        Actors.BindToCloud(getNextSessionStream, options)

let private getConnectionString = function
| WorkerCloudConnection (ConnectionString connection, Queue queue) -> connection, queue
| ActorCloudConnection (ConnectionString connection, Queue queue) -> connection, queue

/// Posts a message to a pool of workers.
let SendToWorkerPool<'a> connection (message:'a) = postMessage (createMessageDispatcher <| getConnectionString connection) None message
/// Posts a message to a pool of actors.
let SendToActorPool<'a> connection (ActorKey recipient) (message:'a) = postMessage (createMessageDispatcher <| getConnectionString connection) (Some recipient) message
