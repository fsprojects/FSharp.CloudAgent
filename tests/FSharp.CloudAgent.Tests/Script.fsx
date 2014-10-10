#I @"..\..\src\FSharp.CloudAgent\"
#I @"..\..\packages\"

#r @"WindowsAzure.ServiceBus.2.4.4.0\lib\net40-full\Microsoft.ServiceBus.dll"
#r @"Newtonsoft.Json.6.0.5\lib\net45\Newtonsoft.Json.dll"
#r @"System.Runtime.Serialization.dll"
#load "Types.fs"
#load "Actors.fs"
#load "Messaging.fs"
#load "ConnectionFactory.fs"

open FSharp.CloudAgent
open FSharp.CloudAgent.Connections
open FSharp.CloudAgent.Messaging

// Connection strings to different service bus queues
let connectionString = ConnectionString "Endpoint=sb://yourSb.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=key"
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
ConnectionFactory.StartListening(workerConn, createBasicAgent >> BasicCloudAgent)
let postToCloud = ConnectionFactory.SendToWorkerCloud workerConn
postToCloud { Name = "Isaac"; Age = 34 } |> Async.RunSynchronously







(* ------------- Resilient F# Agent using Service Bus to ensure processing of messages --------------- *)

let createResilientAgent (ActorKey actorKey) =
    new ResilientMailboxProcessor<Person>(fun mailbox ->
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

ConnectionFactory.StartListening(workerConn, createResilientAgent >> ResilientCloudAgent)
postToCloud { Name = "Isaac"; Age = 34 } |> Async.RunSynchronously




(* ------------- Actor-based F# Agents using Service Bus sessions to ensure synchronisation of messages--------------- *)

ConnectionFactory.StartListening(actorConn, createResilientAgent >> ResilientCloudAgent)
let postToFred = ConnectionFactory.SendToActorCloud actorConn (ActorKey "Fred")
postToFred { Name = "Tim"; Age = 34 } |> Async.RunSynchronously

