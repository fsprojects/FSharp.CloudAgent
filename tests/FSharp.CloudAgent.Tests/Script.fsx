#I @"..\..\src\FSharp.CloudAgent\"
#I @"..\..\packages\"

#r @"WindowsAzure.ServiceBus\lib\net40-full\Microsoft.ServiceBus.dll"
#r @"Newtonsoft.Json\lib\net45\Newtonsoft.Json.dll"
#r @"System.Runtime.Serialization.dll"
#load "Types.fs"
#load "Actors.fs"
#load "Messaging.fs"
#load "ConnectionFactory.fs"

open FSharp.CloudAgent
open FSharp.CloudAgent.Connections
open FSharp.CloudAgent.Messaging

// Connection strings to different service bus queues
let connectionString = ServiceBusConnection "Endpoint=sb://yourServiceBus.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=yourKey"
let workerConn = WorkerCloudConnection(connectionString, Queue "workerQueue")
let actorConn = ActorCloudConnection(connectionString, Queue "actorQueue")

type Person = { Name : string; Age : int }



(* ------------- Standard F# Agent --------------- *)

let createBasicAgent (ActorKey actorKey) =
    new MailboxProcessor<Person>(fun mailbox ->
        async {
            while true do
                let! message = mailbox.Receive()                
                printfn "Actor %s has received message '%A', processing..." actorKey message
        })

// Start listening for messages without any error resilience
let disposable = ConnectionFactory.StartListening(workerConn, createBasicAgent >> BasicCloudAgent)

// Send a message to the worker pool
{ Name = "Isaac"; Age = 34 }
|> ConnectionFactory.SendToWorkerPool workerConn
|> Async.RunSynchronously

disposable.Dispose()






(* ------------- Resilient F# Agent using Service Bus to ensure processing of messages --------------- *)

let createResilientAgent (ActorKey actorKey) =
    MailboxProcessor.Start(fun mailbox ->
        async {
            while true do
                let! message, reply = mailbox.Receive()                
                printfn "Actor %s has received message '%A', processing..." actorKey message
                let response =
                    match message with
                    | { Name = "Isaac" } -> Completed
                    | { Name = "Mike" } -> Abandoned
                    | _ -> Failed
                printfn "%A" response
                reply response
        })

// Start listening for messages with built-in error resilience
ConnectionFactory.StartListening(workerConn, createResilientAgent >> ResilientCloudAgent)

// Send a message to the worker pool
{ Name = "Isaac"; Age = 34 } 
|> ConnectionFactory.SendToWorkerPool workerConn
|> Async.RunSynchronously




(* ------------- Actor-based F# Agents using Service Bus sessions to ensure synchronisation of messages--------------- *)

// Start listening for actor messages with built-in error resilient
let actorDisposable = ConnectionFactory.StartListening(actorConn, createResilientAgent >> ResilientCloudAgent)

// Send a message 
{ Name = "Isaac"; Age = 34 } 
|> ConnectionFactory.SendToActorPool actorConn (ActorKey "Tim")
|> Async.RunSynchronously

actorDisposable.Dispose()