namespace FSharp.CloudAgent

/// Represents a unique key to identify an agent / actor.
type ActorKey = 
    | ActorKey of string

[<AutoOpen>]
module internal Async = 
    open System
    open System.Threading.Tasks
    
    let AwaitTaskEmpty(task : Task) = 
        Async.FromContinuations(fun (onSuccess, onException, onCancellation) -> 
            task.ContinueWith(fun t -> 
                if t.IsCompleted then onSuccess()
                elif t.IsFaulted then onException (t.Exception)
                else onCancellation (System.OperationCanceledException()))
            |> ignore)
    
    type AsyncResult<'T> = 
        | Error of Exception : Exception
        | Result of Result : 'T
    
    let CatchException workflow = 
        async { 
            let! result = workflow |> Async.Catch
            return match result with
                   | Choice1Of2 result -> Result result
                   | Choice2Of2 ex -> Error ex
        }
namespace FSharp.CloudAgent.Connections

/// Represents details of a connection to an Azure Service Bus.
type ConnectionString = 
    | ConnectionString of string

/// Represents a service bus queue.
type Queue = 
    | Queue of string

/// Represents a connection to a pool of agents.
type CloudConnection = 
    /// A generic worker cloud that can run workloads in parallel.
    | WorkerCloudConnection of ConnectionString * Queue
    /// An actor-based cloud that can run workloads in parallel whilst ensuring sequential workloads per-actor.
    | ActorCloudConnection of ConnectionString * Queue
namespace FSharp.CloudAgent.Messaging

open System

/// The different completion statuses a CloudMessage can have.
type MessageProcessedStatus = 
    /// The message successfully completed.
    | Completed
    /// The message was not processed successfully and should be returned to the queue for processing again.
    | Failed
    /// The message cannot be processed and should be not be attempted again.
    | Abandoned

/// Contains a message to be processed by a ReslientMailboxProcessor as well as a reply channel to confirm message completion.
type ResilientMessage<'a> = 'a * (MessageProcessedStatus -> unit)

/// Represents an agent that can deal with Resilient Messages.
type ResilientMailboxProcessor<'a> = MailboxProcessor<ResilientMessage<'a>>

/// Represents the kinds of F# Agents that can be bound to an Azure Service Bus Queue for processing distributed messages, optionally with automatic retry.
type CloudAgentKind<'a> = 
    /// A simple cloud agent that offers simple forward-only processing of messages.
    | BasicCloudAgent of MailboxProcessor<'a>
    /// A cloud agent that requires explicit completion of processed message, with automatic retry and dead lettering.
    | ResilientCloudAgent of ResilientMailboxProcessor<'a>

/// Contains the raw data of a cloud message.
type internal SimpleCloudMessage = 
    { Body : string
      LockToken : Guid }
