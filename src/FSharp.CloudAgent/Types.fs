namespace FSharp.CloudAgent

/// Represents a unique key to identify an agent / actor.
type ActorKey = 
    /// Represents a unique key to identify an agent / actor.
    | ActorKey of string

[<AutoOpen>]
module internal Async = 
    open System
    
    let (|Result|Error|) =
        function
        | Choice1Of2 result -> Result result
        | Choice2Of2 (ex:Exception) -> Error ex

namespace FSharp.CloudAgent.Connections

/// Represents details of a connection to an Azure Service Bus.
type ServiceBusConnection = 
    /// Represents details of a connection to an Azure Service Bus.
    | ServiceBusConnection of string

/// Represents a service bus queue.
type Queue = 
    /// Represents a service bus queue.
    | Queue of string

/// Represents a connection to a pool of agents.
type CloudConnection = 
    /// A generic worker cloud that can run workloads in parallel.
    | WorkerCloudConnection of ServiceBusConnection * Queue
    /// An actor-based cloud that can run workloads in parallel whilst ensuring sequential workloads per-actor.
    | ActorCloudConnection of ServiceBusConnection * Queue
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

/// Represents the kinds of F# Agents that can be bound to an Azure Service Bus Queue for processing distributed messages, optionally with automatic retry.
type CloudAgentKind<'a> = 
    /// A simple cloud agent that offers simple forward-only processing of messages.
    | BasicCloudAgent of MailboxProcessor<'a>
    /// A cloud agent that requires explicit completion of processed message, with automatic retry and dead lettering.
    | ResilientCloudAgent of MailboxProcessor<'a * (MessageProcessedStatus -> unit)>

/// Contains the raw data of a cloud message.
type internal SimpleCloudMessage = 
    { Body : byte[]
      LockToken : string
      Expiry : DateTime }
