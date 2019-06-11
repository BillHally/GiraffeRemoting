module GiraffeRemoting.App

open System
open System.IO
open System.Runtime.CompilerServices
open System.ServiceProcess

open Microsoft.AspNetCore.Builder
//open Microsoft.AspNetCore.Cors.Infrastructure
open Microsoft.AspNetCore.Hosting
open Microsoft.Extensions.Logging
open Microsoft.Extensions.DependencyInjection
open Microsoft.AspNetCore.Hosting.WindowsServices

//open FSharp.Control.Tasks.V2

open Giraffe

open Shared

type T = class end

let createService (logger : ILogger<T>) =
    let all = ResizeArray<_>()

    {
        ModelOperations.Create =
            fun s ->
                async {
                    logger.LogInformation(sprintf "Create: %s" s)
                    let x =
                        {
                            Model.Value = s
                        }
                        
                    all.Add x
                    
                    return  x
                }

        All = async { return all |> Seq.toArray }
    }

open Fable.Remoting.Server
open Fable.Remoting.Giraffe
open System.Net
open System.Security.Cryptography.X509Certificates
open Microsoft.AspNetCore.Http

type CustomWebHostService(host : IWebHost) =
    inherit WebHostService(host)

    let logger =
        host.Services
            .GetRequiredService<ILogger<CustomWebHostService>>()

    override __.OnStarting(args) =
        logger.LogInformation("OnStarting method called.")
        base.OnStarting(args)

    override __.OnStarted() =
        logger.LogInformation("OnStarted method called.")
        base.OnStarted()

    override __.OnStopping() =
        logger.LogInformation("OnStopping method called.")
        base.OnStopping()
    
[<Extension>]
type WebHostServiceExtensions =
    [<Extension>]
    static member RunAsCustomService(host : IWebHost) =
        let webHostService = new CustomWebHostService(host)
        ServiceBase.Run(webHostService)
        
// ---------------------------------
// Models
// ---------------------------------

type Message =
    {
        Text : string
    }

// ---------------------------------
// Web app
// ---------------------------------

type CustomError = { errorMsg: string }

let errorHandler (ex: Exception) (routeInfo: RouteInfo<_>) = 
    // do some logging
    printfn "Error at %s on method %s" routeInfo.path routeInfo.methodName
    // decide whether or not you want to propagate the error to the client
    match ex with
    | :? System.IO.IOException as ex ->
        let customError = { errorMsg = sprintf "IO: %s" ex.Message }
        Propagate customError
    | ex ->
        let customError = { errorMsg = ex.Message }
        Propagate customError
        //// ignore error
        //Ignore
        
let createWebApp logger : HttpHandler = 
    Remoting.createApi()
    |> Remoting.fromValue (createService logger)
    |> Remoting.withDiagnosticsLogger (printfn "DIAG: %s")
    |> Remoting.withErrorHandler errorHandler
    |> Remoting.buildHttpHandler

// ---------------------------------
// Error handler
// ---------------------------------

let giraffeErrorHandler (ex : Exception) (logger : ILogger) =
    logger.LogError(ex, "An unhandled exception has occurred while executing the request.")
    clearResponse >=> setStatusCode 500 >=> text ex.Message

// ---------------------------------
// Config and Main
// ---------------------------------

let configureApp (app : IApplicationBuilder) : unit =
    //let env = app.ApplicationServices.GetService<IHostingEnvironment>()
    let logger = app.ApplicationServices.GetRequiredService<ILogger<T>>()

    logger.LogInformation("Configuring app...")
    
    //(
    //    match env.IsDevelopment() with
    //    | true  -> app.UseDeveloperExceptionPage()
    //    | false -> app.UseGiraffeErrorHandler giraffeErrorHandler
    //)
    app.UseSignalR(fun routes -> routes.MapHub<Hubs.ChatHub>(PathString("/ChatHub")) |> ignore)
        .UseGiraffeErrorHandler(giraffeErrorHandler)
        .Use(
                fun context next ->
                    if not context.Request.IsHttps then
                        context.Abort()

                    next.Invoke()
            )
        .UseGiraffe(createWebApp logger)

let configureServices (services : IServiceCollection) =
//    services.AddCors()    |> ignore
    services.AddGiraffe() |> ignore
    services.AddSignalR() |> ignore

let configureLogging runAsService (builder : ILoggingBuilder) =
    builder.AddFilter(fun l -> l >= LogLevel.Debug)
           .AddTraceSource(Diagnostics.SourceSwitch("TraceSwitch", "Verbose"))
           .AddDebug()
           |> ignore

    if runAsService then
        builder.AddEventLog()
           |> ignore
    else
        builder.AddConsole()
           |> ignore

//let getCertificate (location : StoreLocation) (storeName : StoreName) (subject : string) : X509Certificate2 =
//    use store = new X509Store(storeName, location)
//    store.Open(OpenFlags.ReadOnly)

//    let certs = store.Certificates.Find(X509FindType.FindBySubjectName, subject, false)

//    match certs |> Seq.cast<X509Certificate2> |> Seq.tryHead with
//    | None   -> failwithf "Failed to find certificate %A in %A (at %A)" subject storeName location
//    | Some x -> x

[<EntryPoint>]
let main args =
    try
        //let certificate = getCertificate StoreLocation.LocalMachine StoreName.Root "localhost"

        //printfn "------------------------------------------------------------"
        //printfn "%A" certificate
        //printfn "------------------------------------------------------------"

        let pathToAssembly = System.Reflection.Assembly.GetExecutingAssembly().Location
        let contentRoot = Path.GetDirectoryName(pathToAssembly)
        let webRoot     = Path.Combine(contentRoot, "WebRoot")

        let runAsService =
            not (Array.contains "--run" args) && Environment.UserInteractive

        let host =
            WebHostBuilder()
                .UseKestrel( fun options ->
                    options.Listen
                        (
                            IPAddress.Loopback,
                            5001,
                            
                            fun listenOptions ->
                                listenOptions.UseHttps(StoreName.Root, "localhost", false, StoreLocation.LocalMachine)
                                |> ignore
                        )
                )
                .UseContentRoot(contentRoot)
                //.UseIISIntegration()
                .UseWebRoot(webRoot)
                .Configure(Action<IApplicationBuilder> configureApp)
                .ConfigureServices(configureServices)
                .ConfigureLogging(configureLogging runAsService)
                .Build()

        let tr s = Diagnostics.Trace.WriteLine(sprintf "GiraffeRemoting: %s" s)
        tr (sprintf "           args: %A" args)
        tr (sprintf "UserInteractive: %b" Environment.UserInteractive)
        tr (sprintf "   runAsService: %b" runAsService)

        if runAsService then
            host.RunAsCustomService()
        else
            host.Run()

        0
    with
    | ex ->
        printfn "%A" ex
        //Diagnostics.Debugger.Launch() |> ignore
        1
