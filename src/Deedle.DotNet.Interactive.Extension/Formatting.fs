namespace Deedle.DotNet.Interactive.Extension

module Formatting =
    open Deedle
    open System.Collections.Generic
    open Microsoft.DotNet.Interactive.Formatting

    let toTabularJsonData (frame: Frame<'a, 'b>) : TabularJsonString =
        let toDataDict (frame: Frame<'a, 'b>) =
            let colKeys = frame.ColumnKeys

            frame.Rows
            |> Series.observations
            |> Seq.map
                (fun (_, row) ->
                    let dataDict = Dictionary<string, obj>()

                    colKeys
                    |> Seq.iter (fun key -> dataDict.Add(string key, row.Get key))

                    dataDict)

        let fields =
            Seq.zip (frame.ColumnKeys |> Seq.map string) frame.ColumnTypes
            |> readOnlyDict

        let dataDict = toDataDict frame
        TabularJsonString.Create(fields, dataDict)
