namespace Deedle.DotNet.Interactive.Extension

open System
open System.Threading.Tasks
open System.IO
open Microsoft.DotNet.Interactive
open Microsoft.DotNet.Interactive.Formatting
open Microsoft.DotNet.Interactive.FSharp.FSharpKernelHelpers.Html
open Deedle
open Deedle.Internal

type DeedleFormatterExtension() =
    let maxRows = 20
    let maxCols = 15

    let (|SeriesValues|_|) (value: obj) =
        let iser =
            value.GetType().GetInterface("ISeries`1")

        if not (isNull iser) then
            let keys =
                value
                    .GetType()
                    .GetProperty("Keys")
                    .GetValue(value)
                :?> System.Collections.IEnumerable

            let vector =
                value
                    .GetType()
                    .GetProperty("Vector")
                    .GetValue(value)
                :?> IVector

            Some(Seq.zip (Seq.cast<obj> keys) vector.ObjectSequence)
        else
            None

    // TODO: make this configurable for floats and such
    let formatValue (def: string) =
        function
        | Some v -> v.ToString()
        | None -> def

    let determineType (nullable: 'a) = typeof<'a>

    /// Super opinionated because I know what the pattern looks like
    /// Don't trust me on this
    let peekAtSeriesTypes (s: ((obj * OptionalValue<obj>) seq)) =
        if Seq.isEmpty s then
            None
        else
            let (k, v) = Seq.head s

            (k.GetType(), determineType v.ValueOrDefault)
            |> Some

    // for some reason the return type was changed in the formatting package
    let eraseSeqType (s: seq<'a>) = s :?> seq<Object>

    let getHtml (formattable: IFsiFormattable) =
        match formattable with
        | SeriesValues s ->
            let typeInfo =
                peekAtSeriesTypes s
                |> function
                | None -> String.Empty
                | Some (keyType, _) ->
                    // deedle optionals are hard to inspect so I rather don't show them at all
                    $"Key type: %A{keyType}"

            let entries = Seq.length s
            let toBeShown = Seq.take (min maxCols entries) s

            div [] [
                table [] [
                    caption [] [
                        str $"A series: %i{entries} values. %s{typeInfo}"
                    ]
                    thead [] [
                        thead [] [
                            th [] [ str "Keys" ]
                            yield!
                                toBeShown
                                |> Seq.map (fun kvp -> th [] [ str (fst kvp |> string) ])
                                |> eraseSeqType
                            if entries > maxCols then
                                th [] [ str "..." ]
                        ]
                    ]
                    tbody [] [
                        td [] [ str "Values" ]
                        yield!
                            toBeShown
                            |> Seq.map (fun kvp -> td [] [ str (snd kvp |> string) ])
                            |> eraseSeqType
                        if entries > maxCols then
                            th [] [ str "..." ]
                    ]
                ]
            ]
            |> Some
        | :? IFrame as df ->
            { new IFrameOperation<_> with
                member x.Invoke(df: Frame<_, _>) =
                    let keyRepresentations = df.ColumnKeys |> Seq.map (sprintf "%A")

                    let typeRepresenations = df.ColumnTypes |> Seq.map string

                    let keysAndTypes =
                        (keyRepresentations, typeRepresenations)
                        ||> Seq.zip

                    let rowCount = df.RowCount
                    let columnCount = keyRepresentations |> Seq.length

                    let notShownRows = rowCount - maxRows |> max 0
                    let notShownColumns = columnCount - maxCols |> max 0

                    let rowSummary =
                        if notShownRows < 1 then
                            None
                        else
                            Some $"%i{notShownRows} additional rows"

                    let columnSummary =
                        if notShownColumns < 1 then
                            None
                        else
                            keysAndTypes
                            |> Seq.skip maxCols
                            |> Seq.map
                                (fun (k, v) ->
                                    span [] [
                                        str " "
                                        b [] [ str k ]
                                        small [] [ str $" <%s{v}>" ]
                                        str " "
                                    ])
                            |> Some

                    let summary =
                        match (rowSummary, columnSummary) with
                        | None, None -> None
                        | Some rs, None -> span [] [ str $"...with %s{rs}" ] |> Some
                        | None, Some cs ->
                            span [] [
                                str $"...with %i{notShownColumns} additional variables: "
                                br [] []
                                yield! eraseSeqType cs
                            ]
                            |> Some
                        | Some rs, Some cs ->
                            span [] [
                                str $"...with %s{rs} and %i{notShownColumns} additional variables: "
                                br [] []
                                yield! eraseSeqType cs
                            ]
                            |> Some

                    div [] [
                        table [] [
                            caption [] [
                                str $"A frame: %i{rowCount} x %i{columnCount}"
                            ]
                            thead [] [
                                tr [] [
                                    th [] []
                                    yield!
                                        df.ColumnKeys
                                        |> Seq.take (min maxCols columnCount)
                                        |> Seq.map (fun ck -> th [] [ str (ck.ToString()) ])
                                        |> eraseSeqType
                                    if maxCols < columnCount then
                                        th [] [ str "..." ]
                                ]
                                tr [] [
                                    th [] []
                                    yield!
                                        df.ColumnTypes
                                        |> Seq.take (min maxCols columnCount)
                                        |> Seq.map (fun ct -> th [] [ ct |> string |> str ])
                                        |> eraseSeqType
                                    if maxCols < columnCount then
                                        th [] [ str "..." ]
                                ]
                            ]
                            tbody [] [
                                yield!
                                    df
                                    |> Frame.sliceCols (
                                        df.ColumnKeys
                                        |> Seq.take (min columnCount maxCols)
                                    )
                                    |> Frame.take (min maxRows rowCount)
                                    |> Frame.rows
                                    |> Series.observationsAll
                                    |> Seq.map
                                        (fun item ->
                                            let def, k, data =
                                                match item with
                                                | k, Some d ->
                                                    "N/A", k.ToString(), Series.observationsAll d |> Seq.map snd
                                                | k, _ -> "N/A", k.ToString(), df.ColumnKeys |> Seq.map (fun _ -> None)

                                            let toRow v = td [] [ embedNoContext v ]

                                            let row =
                                                data |> Seq.map (formatValue def >> toRow)

                                            tr [] [
                                                td [] [ embedNoContext k ]
                                                yield! eraseSeqType row
                                                if columnCount > maxCols then
                                                    td [] [ str "..." ]
                                            ])
                                    |> eraseSeqType
                                if rowCount > maxRows then
                                    tr [] [
                                        yield!
                                            fun _ -> td [] [ str "..." ]
                                            |> Seq.init ((min columnCount maxCols) + 1)
                                            |> eraseSeqType
                                    ]
                            ]
                        ]
                        match summary with
                        | Some s -> div [] [ p [] [ s ] ]
                        | None -> ()
                    ]
                    |> Some }
            |> df.Apply
        | _ -> None

    let registerFormatter () =
        Formatter.Register<IFsiFormattable>(
            (fun (formattable: IFsiFormattable) (writer: TextWriter) ->
                match getHtml formattable with
                | Some v -> writer.Write v
                | None -> writer.Write ""),
            mimeType = "text/html"
        )

    interface IKernelExtension with
        member _.OnLoadAsync _ =
            registerFormatter ()

            if isNull KernelInvocationContext.Current |> not then
                let message =
                    let extName = nameof DeedleFormatterExtension
                    let frameName = nameof Frame<_, _>
                    let seriesName = nameof Series<_, _>
                    $"Added %s{extName} including formatters for %s{frameName} and %s{seriesName}"

                KernelInvocationContext.Current.Display(message, [| "text/plain" |])
                |> ignore

            Task.CompletedTask
