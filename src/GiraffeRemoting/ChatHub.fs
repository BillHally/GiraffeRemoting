module Hubs

open Microsoft.AspNetCore.SignalR
open FSharp.Control.Tasks.ContextInsensitive

type ChatHub() =
  inherit Hub()
  member x.SendMessage (user : string, msg : string) =
    task {
      do! x.Clients.All.SendAsync("ReceiveMessage", user, msg)
    }
