[<NUnit.Framework.TestFixture>]
module FSharp.CloudAgent.Tests.``Actor Factory``

open FSharp.CloudAgent
open FSharp.CloudAgent.Actors
open FSharp.CloudAgent.Messaging
open FSharp.CloudAgent.Tests.Helpers
open NUnit.Framework
open System
open Swensen.Unquote

let private getActorStore() = Factory.CreateActorStore(fun _ -> getBasicAgent() |> snd)

let private assertAgentIsStarted (agent:MailboxProcessor<_>) =
    Assertions.raisesWith<InvalidOperationException>
        <@ agent.Start() @>
        (fun ex -> <@ ex.Message = "The MailboxProcessor has already been started." @>)

[<Test>]
let ``ActorStore creates new actors``() =
    let actorStore = getActorStore()
    actorStore.GetActor (ActorKey "isaac") |> ignore

[<Test>]
let ``ActorStore Automatically starts agents``() =
    let actorStore = getActorStore()
    let (BasicCloudAgent isaac) = actorStore.GetActor (ActorKey "isaac")
    isaac |> assertAgentIsStarted

[<Test>]
let ``ActorStore Manages multiple new actors``() =
    let actorStore = getActorStore()
    let isaac = actorStore.GetActor (ActorKey "isaac")
    let tony = actorStore.GetActor (ActorKey "tony")
    isaac <>? tony

[<Test>]
let ``ActorStore Reuses existing actors``() =
    let actorStore = getActorStore()
    let first = actorStore.GetActor (ActorKey "isaac")
    let second = actorStore.GetActor (ActorKey "isaac")
    first =? second

[<Test>]
let ``ActorStore Removes existing actors``() =
    let actorStore = getActorStore()
    let first = actorStore.GetActor (ActorKey "isaac")
    actorStore.RemoveActor (ActorKey "isaac")
    let second = actorStore.GetActor (ActorKey "isaac")
    first <>? second

[<Test>]
let ``ActorStore Disposes of removed agents``() =
    let actorStore = getActorStore()
    let (BasicCloudAgent isaac) = actorStore.GetActor (ActorKey "isaac")
    actorStore.RemoveActor (ActorKey "isaac")

[<Test>]
let ``AgentSelector creates already-started agents``() =
    let selector = Factory.CreateAgentSelector(5, fun _ -> getBasicAgent() |> snd)
    for _ in 1 .. 10 do
        let (BasicCloudAgent agent) = selector() |> Async.RunSynchronously
        agent |> assertAgentIsStarted