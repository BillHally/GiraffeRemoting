open System

open FsToolkit.ErrorHandling

open Shared

open Fable.Remoting.DotnetClient
open System.Threading

// specifies how the routes should be generated
let routes = sprintf "wss://localhost:5001/%s/%s"

// proxy: Proxy<IServer> 
let proxy = Proxy.create<ModelOperations> routes 

let go () =
    asyncResult { 
        let! hello = proxy.callSafely <@ fun server -> server.Create "hello" @>
        printfn "Hello: %s" hello.Value

        let! world = proxy.callSafely <@ fun server -> server.Create "world" @>
        printfn "World: %s" world.Value

        let! all   = proxy.callSafely <@ fun server -> server.All            @>
        printfn "  All: %A" (all |> Array.map (fun x -> x.Value))
    }

let tid () = Thread.CurrentThread.ManagedThreadId

open Microsoft.AspNetCore.SignalR.Client
open System.Threading.Tasks
open FSharp.Control.Tasks.V2

let connectToHub () =
    let connection =
        HubConnectionBuilder()
            .WithUrl("https://localhost:5001/ChatHub")
            .Build()

    connection.add_Closed(
        fun error ->
            task {
                printfn "%2d: Connection closed: %A" (tid ()) error
                do! Task.Delay(Random().Next(0,5) * 1000)
                do! connection.StartAsync()
            } :> Task
        )

    let receptionSubscription =
        connection.On<string, string>
            (
                "ReceiveMessage",
                fun user message ->
                    printfn "%2d: Received: %s: %s" (tid ()) user message
            )

    let connectTask =
        task {
            try
                do! connection.StartAsync()
                printfn "%2d: Connection started" (tid ())
            with
            | ex ->
                printfn "%2d: ERROR: %A" (tid ()) ex
        }

    {|
        Connection            = connection
        ReceptionSubscription = receptionSubscription
        ConnectTask           = connectTask
    |}

let sendMessage (connection : HubConnection) (user : string) (msg : string) =
    task {
        try
            printfn "%2d: Sending : %s: %s" (tid ()) user msg
            do! connection.InvokeAsync("SendMessage", user, msg)
        with
        | ex ->
            printfn "%2d: ERROR: %A" (tid ()) ex
    }

[<EntryPoint>]
let main argv =
    use gate = new ManualResetEventSlim()

    printfn "%2d: main" (tid ()) 

    async {
        let! result = go ()

        match result with
        | Ok   ()  -> printfn "%2d: OK" (tid ()) 
        | Error ex ->
            eprintfn "%2d: ERROR: %A" (tid ()) ex

        let hub = connectToHub ()
        do! Async.AwaitTask hub.ConnectTask

        do! Async.AwaitTask (sendMessage hub.Connection "UserA" "1st message")
        do! Async.AwaitTask (sendMessage hub.Connection "UserB" "2nd message")

        gate.Set()
    }
    |> Async.StartImmediate

    gate.Wait()

    printfn "%2d: Done" (tid ()) 

    0

