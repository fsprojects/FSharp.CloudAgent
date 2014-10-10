namespace FSharp.CloudAgent.Messaging

open FSharp.CloudAgent
open System
open Newtonsoft.Json

[<AutoOpen>]
module internal Streams = 
    open Microsoft.ServiceBus.Messaging
    open FSharp.CloudAgent

    /// Represents a stream of cloud messages.
    type ICloudMessageStream = 
        abstract GetNextMessage : unit -> Async<SimpleCloudMessage option>
        abstract CompleteMessage : Guid -> Async<unit>
        abstract AbandonMessage : Guid -> Async<unit>
        abstract DeadLetterMessage : Guid -> Async<unit>

    /// Represents a stream of messages for a specific actor.
    type IActorMessageStream = 
        inherit ICloudMessageStream
        abstract RenewSessionLock : unit -> Async<unit>
        abstract SessionId : ActorKey

    type private QueueStream(receiver : MessageReceiver) = 
        interface ICloudMessageStream with            
            member __.DeadLetterMessage(token) = 
                token
                |> receiver.DeadLetterAsync
                |> Async.AwaitTaskEmpty
            
            member __.AbandonMessage(token) = 
                token
                |> receiver.AbandonAsync
                |> Async.AwaitTaskEmpty
            
            member __.CompleteMessage(token) = 
                token
                |> receiver.CompleteAsync
                |> Async.AwaitTaskEmpty
            
            member __.GetNextMessage() = 
                async { 
                    let! message = receiver.ReceiveAsync() |> Async.AwaitTask
                    return Some {   Body = message.GetBody<string>()
                                    LockToken = message.LockToken }
                }
    
    type private SessionisedQueueStream(session : MessageSession) = 
        inherit QueueStream(session)
        interface IActorMessageStream with
            member __.RenewSessionLock() = session.RenewLockAsync() |> Async.AwaitTaskEmpty
            member __.SessionId = ActorKey session.SessionId

    let CreateActorMessageStream (connectionString, queueName) = 
        let queue = QueueClient.CreateFromConnectionString(connectionString, queueName)
        fun () -> 
            async { 
                let! session = queue.AcceptMessageSessionAsync() |> Async.AwaitTask
                return match session with
                        | null -> None
                        | session -> Some(SessionisedQueueStream session :> IActorMessageStream)
            }

    let CreateQueueStream(connectionString, queueName) =
        let queueReceiver = MessagingFactory.CreateFromConnectionString(connectionString).CreateMessageReceiver(queueName)
        QueueStream(queueReceiver) :> ICloudMessageStream

[<AutoOpen>]
module internal Serialization =
    /// Manages serialization / deserialization for putting messages on the queue.
    type ISerializer<'a> =
        /// Deserializes a string back into an object.
        abstract member Deserialize : string -> 'a
        /// Serializes an object into a string.
        abstract member Serialize : 'a -> string
    
    /// A serializer using Newtonsoft's JSON .NET serializer.
    let JsonSerializer<'a>() =
        { new ISerializer<'a> with
                member __.Deserialize(json) = JsonConvert.DeserializeObject<'a>(json)
                member __.Serialize(data) = JsonConvert.SerializeObject(data) }

[<AutoOpen>]
module internal Helpers =
    /// Knows how to process a single brokered message from the service bus, with error handling
    /// and processing by the target queue.
    let ProcessBrokeredMessage<'a> (serializer:ISerializer<'a>) (agent:CloudAgent<'a>) message =
        async {
            let! messageBody = async { return serializer.Deserialize(message.Body) } |> Async.CatchException
            let! processResult =
                match messageBody with
                | Error _ -> async { return Failed } // could not deserialize
                | Result messageBody ->
                    async {
                        let! processingResult = agent.PostAndAsyncReply(fun ch -> messageBody, ch.Reply) |> Async.CatchException
                        return
                            match processingResult with
                            | Error _ -> Failed
                            | Result status -> status
                    }
            return processResult
        }
    
    /// Asynchronously gets the "next" item, repeatedly calling the supply getItem function
    /// until it returns something.
    let rec WithAutomaticRetry getItem pollTime =
        async {
            printfn "Asking stream for more data.."
            let! session = getItem() |> Async.CatchException
            match session with
            | Error _ | Result None ->
                printfn "Nothing returned, sleeping."
                do! Async.Sleep(pollTime)
                return! WithAutomaticRetry getItem pollTime
            | Result (Some value) ->
                printfn "Got something!"
                return value
        }
    
/// Manages dispatching of messages to a service bus queue.
[<AutoOpen>]
module internal Dispatch =
    open Microsoft.ServiceBus.Messaging

    /// Contains configuration details for posting messages to a cloud of agents or actors.
    type MessageDispatcher<'a> = 
        {   Connection : string * string
            Serializer : ISerializer<'a> }

    /// Creates a dispatcher using default settings.
    let createMessageDispatcher<'a> (connectionString, queueName) =
        {   Connection = connectionString, queueName
            Serializer = JsonSerializer<'a>() }

    let postMessage (options:MessageDispatcher<'a>) sessionId message =
        async { 
            use brokeredMessage = 
                let payload = message |> options.Serializer.Serialize
                new BrokeredMessage(payload, SessionId = defaultArg sessionId null)
                            
            let queueClient = QueueClient.CreateFromConnectionString(fst options.Connection, snd options.Connection)
            do! brokeredMessage |> queueClient.SendAsync |> Async.AwaitTaskEmpty
        }
