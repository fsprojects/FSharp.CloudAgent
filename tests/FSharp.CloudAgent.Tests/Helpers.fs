module FSharp.CloudAgent.Tests.Helpers

open FSharp.CloudAgent.Messaging
open System

type Person = { Name : string }
type Thing = { Name : Person }
let internal personSerializer = JsonSerializer<Person>()
let internal createMessage<'a> (serializer:ISerializer<'a>) timeout data =
    { Body = serializer.Serialize data
      LockToken = ""
      Expiry = timeout }
let internal createPersonMessage = createMessage personSerializer (DateTime.UtcNow.AddSeconds 10.)
let internal processPersonMessage = Helpers.ProcessBrokeredMessage personSerializer

let getBasicAgent() =
    let mutable p = { Person.Name = "" }
    p, MailboxProcessor.Start(fun inbox ->
        async {
            while true do 
                let! person = inbox.Receive()
                p <- person
        }) |> BasicCloudAgent

let getResilientAgent handler =
    MailboxProcessor.Start(fun inbox ->
        async {
            while true do 
                let! message, reply = inbox.Receive()
                reply (handler message)
        }) |> ResilientCloudAgent