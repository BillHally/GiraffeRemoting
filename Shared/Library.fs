namespace Shared

open System

type Model = {
    Value : string
}

type ModelOperations =
    {
        Create : string -> Async<Model>
        All    : Async<Model[]>
    }
