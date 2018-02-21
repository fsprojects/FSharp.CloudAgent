[<NUnit.Framework.TestFixture>]
module FSharp.CloudAgent.Tests.``Automatic Retry``

open FSharp.CloudAgent.Messaging
open NUnit.Framework
open Swensen.Unquote

let private toGetNext (sequence:seq<_>) =
    let enumerator = sequence.GetEnumerator()
    fun () ->
        if enumerator.MoveNext() then enumerator.Current
        else async { return None }

[<Test>]
let ``Automatically retries until a result is found``() =
    let getNext =
        seq { yield None; yield None; yield Some 5 }
        |> Seq.map(fun x -> async { return x })
        |> toGetNext
    let value = withAutomaticRetry getNext 0 |> Async.RunSynchronously
    5 =! value

[<Test>]
let ``Automatically handles exceptions``() =
    let getNext = seq { yield async { failwith "foo"; return None }; yield async { return Some 5 } } |> toGetNext
    let value = withAutomaticRetry getNext 0 |> Async.RunSynchronously
    5 =! value