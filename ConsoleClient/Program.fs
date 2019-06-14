open System

open FsToolkit.ErrorHandling

open Shared

open Fable.Remoting.DotnetClient
open System.Threading

// specifies how the routes should be generated
let routes = sprintf "wss://localhost:5001/%s/%s"

// proxy: Proxy<IServer> 
let proxy = Proxy.create<ModelOperations> routes 

let tid () = Thread.CurrentThread.ManagedThreadId

let go () =
    asyncResult { 
        let! hello = proxy.callSafely <@ fun server -> server.Create "hello" @>
        printfn "%d: Hello: %s" (tid ()) hello.Value

        let! world = proxy.callSafely <@ fun server -> server.Create "world" @>
        printfn "%d: World: %s" (tid ()) world.Value

        let! all   = proxy.callSafely <@ fun server -> server.All            @>
        printfn "%d:   All: %A" (tid ()) (all |> Array.map (fun x -> x.Value))
    }

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
        connection.On<string>
            (
                "ReceiveMessage",
                fun message ->
                    printfn "%2d: Received: %s" (tid ()) message
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

let register (connection : HubConnection) =
    task {
        try
            printfn "%2d: Registering as a listener..." (tid ())
            do! connection.InvokeAsync("RegisterListener")
        with
        | ex -> printfn "%2d: ERROR: %A" (tid ()) ex
    }

let sendMessage (connection : HubConnection) (msg : string) =
    task {
        try
            printfn "%2d: %s" (tid ()) msg
            do! connection.InvokeAsync("SendMessage", msg)
        with
        | ex -> printfn "%2d: ERROR: %A" (tid ()) ex
    }

[<EntryPoint>]
let main argv =
    if argv.Length > 1 then
        printfn "Usage: %s [<message>]" (Reflection.Assembly.GetEntryAssembly().GetName().Name)
        1
    else
        use gate = new ManualResetEventSlim()

        printfn "%2d: main" (tid ()) 

        async {
            //let! result = go ()

            //match result with
            //| Ok   ()  -> printfn "%2d: OK" (tid ()) 
            //| Error ex ->
            //    eprintfn "%2d: ERROR: %A" (tid ()) ex

            let hub = connectToHub ()
            do! Async.AwaitTask hub.ConnectTask

            if argv.Length = 0 then
                printfn "Listening..."
                do! Async.AwaitTask (register hub.Connection)
            else
                let message = argv.[0]

                do! Async.AwaitTask (sendMessage hub.Connection message)

                gate.Set()
        }
        |> Async.StartImmediate

        gate.Wait()

        printfn "%2d: Done" (tid ()) 

        0
