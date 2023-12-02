namespace Shoo.Domain

open System

module Constants =
    [<Literal>]
    let ShooFileNameExtension = ".__shoo__"

    [<Literal>]
    let BufferSize = 1024 * 1024

type CreateMode = Create | Replace

type MoveFileStatus = Waiting | Moving | Complete | Failed

type File =
    {
        FullName: string
        FileName: string
        DestinationDirectory: string
        Time: DateTime
        FileSize: int64
        Progress: int
        Status: MoveFileStatus
    }

type CopyOperation =
    {
        Source: string
        FileSize: int64
        Time: DateTime
        Destination: string
        Extension: string
        File: File
    }

type ConfiguredDirectory =
    {
        Path: string
        PathExists: bool
    } with
    static member Empty =
        {
            Path = ""
            PathExists = false
        }