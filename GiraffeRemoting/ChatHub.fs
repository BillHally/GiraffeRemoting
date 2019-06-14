module Hubs

open Microsoft.AspNetCore.SignalR

type ChatHub() =
    inherit Hub()

    member this.RegisterListener () =
        this.Groups.AddToGroupAsync(this.Context.ConnectionId, "Listeners")

    member this.SendMessage (msg : string) =
        this.Clients.Group("Listeners").SendAsync("ReceiveMessage", msg)
