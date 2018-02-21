namespace FSharp.CloudAgent.Messaging

open FSharp.CloudAgent
open Newtonsoft.Json
open System

[<AutoOpen>]
module internal Streams = 
    open Microsoft.Azure.ServiceBus
    open Microsoft.Azure.ServiceBus.Core
    open FSharp.CloudAgent.Messaging

    /// Represents a stream of cloud messages.
    type ICloudMessageStream = 
        abstract GetNextMessage : TimeSpan -> Async<SimpleCloudMessage option>
        abstract CompleteMessage : string -> Async<unit>
        abstract AbandonMessage : string -> Async<unit>
        abstract DeadLetterMessage : string -> Async<unit>

    /// Represents a stream of messages for a specific actor.
    type IActorMessageStream = 
        inherit ICloudMessageStream
        abstract RenewSessionLock : unit -> Async<unit>
        abstract AbandonSession : unit -> Async<unit>
        abstract SessionId : ActorKey

    type private QueueStream(receiver : IMessageReceiver) =
        interface ICloudMessageStream with            
            member __.DeadLetterMessage(token) = 
                token
                |> receiver.DeadLetterAsync
                |> Async.AwaitTask
            
            member __.AbandonMessage(token) = 
                token
                |> receiver.AbandonAsync
                |> Async.AwaitTask
            
            member __.CompleteMessage(token) = 
                token
                |> receiver.CompleteAsync
                |> Async.AwaitTask
            
            member __.GetNextMessage(timeout) = 
                async { 
                    let! message = receiver.ReceiveAsync(timeout) |> Async.AwaitTask
                    match message with
                    | null -> return None
                    | message ->
                        return Some { Body = message.Body
                                      LockToken = "" //message.LockToken
                                      Expiry = message.ExpiresAtUtc }
                }
    
    type private SessionisedQueueStream(session : IMessageSession) =
        inherit QueueStream(session)
        interface IActorMessageStream with
            member __.AbandonSession() = session.CloseAsync() |> Async.AwaitTask
            member __.RenewSessionLock() = session.RenewSessionLockAsync() |> Async.AwaitTask
            member __.SessionId = ActorKey session.SessionId

    let CreateActorMessageStream (connectionString:string, queueName, timeout:TimeSpan) =
        let queue = QueueClient(connectionString, queueName)
        fun () -> 
            async {
                return None
                // let! session = queue.AcceptMessageSessionAsync(timeout) |> Async.AwaitTask |> Async.Catch
                // return
                //     match session with
                //     | Error _
                //     | Result null -> None
                //     | Result session -> Some(SessionisedQueueStream (session) :> IActorMessageStream)
            }

    let CreateQueueStream(connectionString, queueName) =
        // let queueReceiver = MessagingFactory(connectionString).CreateMessageReceiver(queueName)
        // QueueStream(queueReceiver) :> ICloudMessageStream
        failwith "Boom!"

[<AutoOpen>]
module internal Serialization =
    /// Manages serialization / deserialization for putting messages on the queue.
    type ISerializer<'a> =
        /// Deserializes a string back into an object.
        abstract member Deserialize : byte[] -> 'a
        /// Serializes an object into a string.
        abstract member Serialize : 'a -> byte[]
    
    /// A serializer using Newtonsoft's JSON .NET serializer.
    let JsonSerializer<'a>() =
        { new ISerializer<'a> with
            member __.Deserialize(json) = json |> System.Text.Encoding.UTF8.GetString |> JsonConvert.DeserializeObject<'a>
            member __.Serialize(data) = data |> JsonConvert.SerializeObject |> System.Text.Encoding.UTF8.GetBytes }

[<AutoOpen>]
module internal Helpers =
    open FSharp.CloudAgent.Messaging

    /// Knows how to process a single brokered message from the service bus, with error handling
    /// and processing by the target queue.
    let ProcessBrokeredMessage<'a> (serializer:ISerializer<'a>) (agent:CloudAgentKind<'a>) message =
        async {
            let! messageBody = async { return serializer.Deserialize(message.Body) } |> Async.Catch
            let! processResult =
                match messageBody with
                | Error _ -> async { return Failed } // could not deserialize
                | Result messageBody ->
                    async {
                        match agent with
                        | BasicCloudAgent agent ->
                            // Do not wait for a response - just return success.
                            agent.Post messageBody
                            return Completed
                        | ResilientCloudAgent agent ->
                            // Wait for the response and return it. Timeout is set based on message expiry unless it's too large.
                            let expiryInMs =
                                match (int ((message.Expiry - DateTime.UtcNow).TotalMilliseconds)) with
                                | expiryMs when expiryMs < -1 -> -1
                                | expiryMs -> expiryMs

                            let! processingResult = agent.PostAndTryAsyncReply((fun ch -> messageBody, ch.Reply), expiryInMs) |> Async.Catch
                            return
                                match processingResult with
                                | Error _
                                | Result None -> Failed
                                | Result (Some status) -> status
                    }
            return processResult
        }
    
    /// Asynchronously gets the "next" item, repeatedly calling the supply getItem function
    /// until it returns something.
    let withAutomaticRetry getItem pollTime =
        let rec continuePolling() =
            async {
                let! nextItem = getItem() |> Async.Catch
                match nextItem with
                | Error ex -> return! continuePolling()
                | Result None -> return! continuePolling()
                | Result (Some item) -> return item
            }
        continuePolling()
    
/// Manages dispatching of messages to a service bus queue.
[<AutoOpen>]
module internal Dispatch =
    open Microsoft.Azure.ServiceBus

    /// Contains configuration details for posting messages to a cloud of agents or actors.
    type MessageDispatcher<'a> = 
        {   ServiceBusConnectionString : string
            QueueName : string
            Serializer : ISerializer<'a> }

    /// Creates a dispatcher using default settings.
    let createMessageDispatcher<'a> (connectionString, queueName) =
        {   ServiceBusConnectionString = connectionString
            QueueName = queueName
            Serializer = JsonSerializer<'a>() }

    let private toBrokeredMessage options sessionId message =
        let payload = message |> options.Serializer.Serialize
        Message(payload, SessionId = defaultArg sessionId null)

    let postMessages (options:MessageDispatcher<'a>) sessionId messages =
        let toBrokeredMessage = toBrokeredMessage options sessionId
        async { 
            let brokeredMessages = 
                messages
                |> Seq.map toBrokeredMessage 
                |> Seq.toArray
            
            let queueClient = QueueClient(options.ServiceBusConnectionString, options.QueueName)
            do! brokeredMessages |> queueClient.SendAsync |> Async.AwaitTask
        }

    let postMessage (options:MessageDispatcher<'a>) sessionId message =
        async { 
            let brokeredMessage = message |> toBrokeredMessage options sessionId
            let queueClient = QueueClient(options.ServiceBusConnectionString, options.QueueName)
            do! [| brokeredMessage |] |> queueClient.SendAsync |> Async.AwaitTask
        }
