(*** hide ***) 
#r @"..\..\bin\FSharp.CloudAgent.dll"

(**
Tutorial
============
Getting up and running
----------------------
Creating a simple distributable Agent is as simple as creating a regular agent, wrapping it
in a generator function and then binding that function to a service bus queue.

*)

open FSharp.CloudAgent
open FSharp.CloudAgent.Messaging
open FSharp.CloudAgent.Connections

// Standard Azure Service Bus connection string
let serviceBusConnection = ServiceBusConnection "servicebusconnectionstringgoeshere"

// A DTO
type Person = { Name : string; Age : int }

// A function which creates an Agent on demand.
let createASimpleAgent agentId =
    MailboxProcessor.Start(fun inbox ->
        async {
            while true do
                let! message = inbox.Receive()
                printfn "%s is %d years old." message.Name message.Age
        })

// Create a worker cloud connection to the Service Bus Queue "myMessageQueue"
let cloudConnection = WorkerCloudConnection(serviceBusConnection, Queue "myMessageQueue")

// Start listening! A local pool of agents will be created that will receive messages.
// Service bus messages will be automatically deserialised into the required message type.
ConnectionFactory.StartListening(cloudConnection, createASimpleAgent >> BasicCloudAgent)


(**

Posting messages to agents
--------------------------
You can elect to natively send messages to the service bus (as long as the serialisation
format matches i.e. JSON .NET). However, FSharp.CloudAgent includes some handy helper functions
to do this for you.

*)

let sendToMyMessageQueue = ConnectionFactory.SendToWorkerPool cloudConnection

// These messages will be processed in parallel across the worker pool.
sendToMyMessageQueue { Name = "Isaac"; Age = 34 }
sendToMyMessageQueue { Name = "Michael"; Age = 32 }
sendToMyMessageQueue { Name = "Sam"; Age = 27 }

(**

Creating Resilient Agents
-------------------------
We can also create "Resilient" agents. These are standard F# Agents that take advantage of the
Reply Channel functionality inherent in F# agents to allow us to signal back to Azure whether
or not a message was processed correctly or not, which in turn gives us automatic retry and dead
letter functionality that is included with Azure Service Bus.

*)

// A function which creates a Resilient Agent on demand.
let createAResilientAgent agentId =
    MailboxProcessor.Start(fun inbox ->
        async {
            while true do
                let! message, replyChannel = inbox.Receive()
                printfn "%s is %d years old." message.Name message.Age
                
                match message with
                | { Name = "Isaac" } -> replyChannel Completed // all good, message was processed
                | { Name = "Richard" } -> replyChannel Failed // error occurred, try again
                | _ -> replyChannel Abandoned // give up with this message.
        })

// Start listening! A local pool of agents will be created that will receive messages.
ConnectionFactory.StartListening(cloudConnection, createAResilientAgent >> ResilientCloudAgent)

(**

Creating distributed Actors
---------------------------
In addition to the massively distributable messages seen above, we can also create agents which are
threadsafe not only within the context of a process but also in the context of multiple processes on
different machines. This is made possible by Azure Service Bus session support. To turn an agent into
an actor, we simply change the connection and the way in which we post messages.

*)

// Create an actor cloud connection to the Service Bus Queue "myActorQueue". This queue must have
// Session support turned on.
let actorConnection = ActorCloudConnection(serviceBusConnection, Queue "myActorQueue")

// Start listening! Agents will be created as required for messages sent to new actors.
ConnectionFactory.StartListening(actorConnection, createASimpleAgent >> BasicCloudAgent)

// Send some messages to the actor pool
let sendToMyActorQueue = ConnectionFactory.SendToActorPool actorConnection

let sendToFred = sendToMyActorQueue (ActorKey "Fred")
let sendToMax = sendToMyActorQueue (ActorKey "Max")

// The first two messages will be sent in sequence to the same agent in the same process.
// The last message will be sent to a different agent, potentially in another process.
sendToFred { Name = "Isaac"; Age = 34 }
sendToFred { Name = "Michael"; Age = 32 }
sendToMax { Name = "Sam"; Age = 27 }