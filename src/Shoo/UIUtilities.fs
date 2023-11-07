namespace Shoo.UIUtilities

open System

open Avalonia.Data.Converters

type BytesToMegabytesConverter() =
    static member Instance = BytesToMegabytesConverter() :> IValueConverter

    interface IValueConverter with
        member this.Convert(value: obj, targetType: Type, parameter: obj, culture: Globalization.CultureInfo): obj =
            match value with
            | :? int64 as size -> (System.Convert.ToDouble size) / (1024. * 1024.)
            | _ -> 0.
            :> obj

        member this.ConvertBack(value: obj, targetType: Type, parameter: obj, culture: Globalization.CultureInfo): obj =
            raise (NotSupportedException())
