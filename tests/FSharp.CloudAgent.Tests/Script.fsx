#I @"..\..\src\FSharp.CloudAgent\"
#I @"..\..\packages\"

#r @"WindowsAzure.ServiceBus.2.4.4.0\lib\net40-full\Microsoft.ServiceBus.dll"
#r @"Newtonsoft.Json.6.0.5\lib\net45\Newtonsoft.Json.dll"
#r @"System.Runtime.Serialization.dll"
#load "Types.fs"
#load "Actors.fs"
#load "Messaging.fs"
#load "Connectivity.fs"

open FSharp.CloudAgent
open FSharp.CloudAgent.Connections
open FSharp.CloudAgent.Messaging

type Person = { Name : string; Age : int }

let createDummyAgent (ActorKey actorKey) =
    new CloudAgent<Person>(fun mailbox ->
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

let connectionString = ConnectionString "Endpoint=sb://yourservicebus.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=yourkey"

let actorConn = ActorCloudConnection(connectionString, Queue "sessionisedQueue")
ConnectionFactory.StartListening(actorConn, createDummyAgent)
let postToFred = ConnectionFactory.SendToActorCloud actorConn (ActorKey "Fred")
postToFred { Name = "Isaac"; Age = 34 } |> Async.RunSynchronously

let workerConn = WorkerCloudConnection(connectionString, Queue "workerQueue")
ConnectionFactory.StartListening(workerConn, createDummyAgent)
let postToCloud = ConnectionFactory.SendToWorkerCloud(workerConn)
postToCloud { Name = "Isaac"; Age = 34 } |> Async.RunSynchronously