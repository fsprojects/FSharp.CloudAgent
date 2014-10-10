namespace FSharp.CloudAgent.Actors

open System.Collections.Generic
open System
open FSharp.CloudAgent
open FSharp.CloudAgent.Messaging

/// Manages lifetime of actors.
type internal IActorStore<'a> =
    /// Requests an actor for a particular key.
    abstract member GetActor : ActorKey -> CloudAgent<'a>
    /// Tells the store that an actor is no longer required and can be safely removed.
    abstract member RemoveActor : ActorKey -> unit
    
// Manages per-session actors
type private ActorStoreRequest =
| Get of ActorKey
| Remove of ActorKey

type private ActorFactory<'a> = MailboxProcessor<ActorStoreRequest * (AsyncReplyChannel<CloudAgent<'a>> option)>

module internal Factory =
    let private createActorFactory<'a> createAgent =
        let actorStore =
            new ActorFactory<'a>
                (fun inbox ->
                    let actors = Dictionary<string, CloudAgent<'a>>()
                    async {
                        while true do
                            let! message, replyChannel = inbox.Receive()
                            match message with
                            | Get (ActorKey agentId) ->
                                let actor =
                                    if not (actors.ContainsKey agentId) then
                                        printfn "Creating actor %s" agentId
                                        let actor = createAgent (ActorKey agentId)
                                        actors.Add(agentId, actor)
                                        try actor.Start() with _ -> ()                  
                                    actors.[agentId]
                                replyChannel
                                |> Option.iter(fun replyChannel -> replyChannel.Reply actor)
                            | Remove (ActorKey actorKey) ->
                                if actors.ContainsKey(actorKey) then
                                    printfn "Removing actor %s" actorKey
                                    let actorToRemove = actors.[actorKey] :> IDisposable
                                    actors.Remove actorKey |> ignore
                                    actorToRemove.Dispose()
                                        
                    })
        actorStore.Start()
        actorStore
        
    /// Creates an IActorStore that can add / retrieve / remove agents in a threadsafe manner, using the supplied function to create new agents on demand.
    let CreateActorStore<'a> createActor =
        let actorFactory = createActorFactory<'a> createActor
        { new IActorStore<'a> with
            member __.GetActor(sessionId) = actorFactory.PostAndReply(fun ch -> (Get sessionId), Some ch)
            member __.RemoveActor(sessionId) = actorFactory.Post(Remove sessionId, None) }
        
    /// Selects an agent to consume a message.
    type AgentSelectorFunc<'a> = unit -> Async<CloudAgent<'a>>
    
    /// Generates a pool of CloudAgents of a specific size that can be used to select an agent when required.
    let CreateAgentSelector<'a> (size, createAgent : ActorKey -> CloudAgent<'a>) : AgentSelectorFunc<'a> = 
        let agents = 
            [ for i in 1..size ->
                let agent = createAgent (ActorKey (i.ToString()))
                try agent.Start() with _ -> ()
                agent ]
        
        let r = Random()
        fun () -> async { return agents.[r.Next(0, agents.Length)] }