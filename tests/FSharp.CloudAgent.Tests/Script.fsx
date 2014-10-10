﻿#I @"..\..\src\FSharp.CloudAgent\"
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

// Send a message to the worker pool
{ Name = "Isaac"; Age = 34 }
|> ConnectionFactory.SendToWorkerCloud workerConn
|> Async.RunSynchronously







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

// Start listening for messages with built-in error resilience
ConnectionFactory.StartListening(workerConn, createResilientAgent >> ResilientCloudAgent)

// Send a message to the worker pool
{ Name = "Isaac"; Age = 34 } 
|> ConnectionFactory.SendToWorkerCloud workerConn
|> Async.RunSynchronously




(* ------------- Actor-based F# Agents using Service Bus sessions to ensure synchronisation of messages--------------- *)

// Start listening for actor messages with built-in error resilient
ConnectionFactory.StartListening(actorConn, createResilientAgent >> ResilientCloudAgent)

// Send a message 
{ Name = "Isaac"; Age = 34 } 
|> ConnectionFactory.SendToActorCloud actorConn (ActorKey "Fred")
|> Async.RunSynchronously

