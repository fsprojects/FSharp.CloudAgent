(**
About Windows Service Bus
=======================
Windows Service Bus (WSB) can be thought of as an on-premises version of Azure Service
Bus. As such, everything mentioned in the [Azure Service Bus](azure-service-bus.html) section still holds true
in terms of operations and application within Cloud Agent. The difference with WSB is
that you manage the service bus yourself on your network, and can host all entire
internally on your network. This article will provide some simple instructions on
installation of Windows Service Bus that will enable you to use Cloud Agent without
using Azure.

Windows Service Bus versions
----------------------------
WSB currently comes in two versions - 1.0 and 1.1. Whilst 1.0 at the current time of
writing is supported natively in Visual Studio 2013, 1.1 is not. However, WSB 1.1
brings a raft of [features] [wsb-11] that bring it up to parity with the 2.x SDK of Azure Service
Bus. As such, WSB 1.1 is recommended for use with Cloud Agent.

Windows Service Bus Installation
--------------------------------
Full details can be found [here] [install-wsb]. Essentially, you install the WSB service, which stores data
in a SQL database. This can be full SQL Server or SQL Server Express (indeed, you can even
install it on LocalDB).

Managing WSB
------------
Unlike Azure Service Bus, you cannot use the Azure Portal to manage a locally hosted
Windows Service Bus. Instead, there are several ways to interact with WSB: -

* You can use the existing Service Bus SDK to programmatically create queues etc. having
connected to the local service bus.
* You can use the [Service Bus Explorer] [sb-exp] tool. Note that only version 2.1 is 
compatible with WSB 1.1. The latest version (2.5.3) does NOT support WSB 1.1.
* You can use the [Service Bus Powershell cmdlets] [sb-psh] to perform basic interrogation
of your service bus farm.

WSB Connection Strings
----------------------
Connection strings on WSB are similar to those on Azure Service Bus.

1. You can identify the default namespace using the ``get-sbNamespace`` cmdlet.
2. You can generate a connection string using the ``get-sbClientConfiguration`` cmdlet.

This connection string can be used directly in place of the standard Azure Service Bus
connection string in CloudAgent. Your application code remains unchanged.

[wsb-11]: http://msdn.microsoft.com/en-us/library/dn282143.aspx
[install-wsb]: http://msdn.microsoft.com/en-us/library/dn282152.aspx
[sb-exp]: https://code.msdn.microsoft.com/windowsapps/Service-Bus-Explorer-f2abca5a
[sb-psh]: http://msdn.microsoft.com/en-us/library/jj200653%28v=azure.10%29.aspx
*)