namespace Deedle.DotNet.Interactive.Extension

module Formatting =
    open Deedle
    open System.Collections.Generic
    open Microsoft.DotNet.Interactive.Formatting.TabularData

    let toTabularDataResource (frame: Frame<'a, 'b>) : TabularDataResource =
        let toDataDict (frame: Frame<'a, 'b>) =
            let colKeys = frame.ColumnKeys

            frame.Rows
            |> Series.observations
            |> Seq.map
                (fun (_, row) ->
                    let dataDict = Dictionary<string, obj>()

                    colKeys
                    |> Seq.iter (fun key -> dataDict.Add(string key, row.Get key))

                    dataDict :> IDictionary<string, obj>)

        let schema = TableSchema()
        Seq.zip (frame.ColumnKeys |> Seq.map string) frame.ColumnTypes
        |> Seq.iter (fun (key, _type) -> schema.Fields.Add(TableSchemaFieldDescriptor(key, _type.ToTableSchemaFieldType())))

        TabularDataResource(schema, toDataDict frame)
