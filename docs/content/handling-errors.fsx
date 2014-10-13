(**
Handling Errors
===============
Your approach towards error handling will most likely differ depending on the type of agent
pool (irrespective of whether worker or actor) being used.
Basic Agents
------------
Basic agents work on an "at-most-once" delivery mechanism to F# agents. This means that it is
entirely your responsibility to be able to guarantee processing of these messages. You should
cater for logging out failures as well as persisting failed messages to some persistance store
for later inspection. If an agent crashes due to an unhandled exception, your message will be
lost permanently.
Resilient Agents
----------------
Reslient agents work on an "at-least-once" delivery mechanism. Failed messages will automatically
be redelivered until the queue marks them as dead lettered. If an agent crashes due to an unhandled
exception, your message will be automatically re-delivered once it expires. If you set this expiry
too long, you may wish to consider wrapping your agent processing code in a try catch or other
exception handling pattern and immediately returning a Failed to the CloudAgent framework to short-
circuit this wait.
*)