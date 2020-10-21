namespace Deedle.DotNet.Interactive.Extension

// for some reasons the dotnet interactive team has quite odd deps
// make sure to add this myget feed to your sorces
// https://dotnet.myget.org/feed/fsharp/package/nuget/FSharp.Compiler.Private.Scripting/10.7.1-beta.20154.1#
// you'll still crash and burn for the newest versions but you'll get at least a bit more recent
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

    let (|SeriesValues|_|) (value : obj) = 
        let iser = value.GetType().GetInterface("ISeries`1")
        if not (isNull iser) then 
            let keys = 
                value.GetType().GetProperty("Keys").GetValue(value) :?> System.Collections.IEnumerable
            let vector = value.GetType().GetProperty("Vector").GetValue(value) :?> IVector
            Some(Seq.zip (Seq.cast<obj> keys) vector.ObjectSequence)
        else None

    // TODO: make this configurable for floats and such
    let formatValue (def: string) = function
        | Some v -> v.ToString()
        | None -> def

    /// Super opinionated because I know what the pattern looks like
    /// Don't trust me on this
    let peekAtSeriesTypes (s: ((obj * OptionalValue<obj>) seq)) =
        if Seq.isEmpty s then
            None
        else
            let (k, v) = Seq.head s
            (k.GetType(), (v.ValueOrDefault.GetType()))
            |> Some

    let getHtml (formattable: IFsiFormattable) (context: FormatContext) =
        match formattable with
        | SeriesValues s ->
            let typeInfo =
                peekAtSeriesTypes s |> function
                | None -> String.Empty
                | Some (keyType, valueType) ->
                    sprintf "Key type: %A Value type: %A" keyType valueType

            let entries = Seq.length s
            let toBeShown = Seq.take (min maxCols entries) s

            div [] [
                table [] [
                    caption [] [ sprintf "A series: %i values. %s" entries typeInfo |> str ]
                    thead [] [
                        thead [] [
                            th [] [ str "Keys" ]
                            yield! toBeShown
                            |> Seq.map (fun kvp -> th [] [ str (fst kvp |> string) ])
                            if entries > maxCols then th [] [ str "..." ]
                        ]
                    ]
                    tbody [] [
                        td [] [ str "Values" ]
                        yield! toBeShown
                        |> Seq.map (fun kvp -> td [] [ str (snd kvp |> string) ])
                        if entries > maxCols then th [] [ str "..." ]
                    ]
                ]
            ]
            |> Some
        | :? IFrame as df ->
            {
                new IFrameOperation<_> with
                    member x.Invoke(df: Frame<_, _>) =
                        let keyRepresentations =
                            df.ColumnKeys
                            |> Seq.map (sprintf "%A")

                        let typeRepresenations =
                            df.ColumnTypes
                            |> Seq.map string

                        let keysAndTypes =
                            (keyRepresentations, typeRepresenations)
                            ||> Seq.zip

                        let rowCount = df.RowCount
                        let columnCount = keyRepresentations |> Seq.length

                        let notShownRows = rowCount - maxRows |> max 0
                        let notShownColumns = columnCount - maxCols |> max 0

                        let rowSummary =
                            if notShownRows < 1 then None else
                            sprintf "%i rows" notShownRows |> Some

                        let columnSummary =
                            if notShownColumns < 1 then None else
                            keysAndTypes
                            |> Seq.skip maxCols
                            |> Seq.map (fun (k, v) ->
                                span [ ] [
                                    str " "
                                    b [] [ str k ]
                                    small [] [ sprintf " <%s>" v |> str ]
                                    str " "
                                ])
                            |> Some

                        let summary =
                            match (rowSummary, columnSummary) with
                            | None, None -> None
                            | Some rs, None ->
                                span [] [ sprintf "...with %s additional rows" rs |> str ]
                                |> Some
                            | None, Some cs ->
                                span [] [
                                    sprintf "...with %i additional variables: " notShownColumns |> str
                                    br [] []
                                    yield! cs
                                ]
                                |> Some
                            | Some rs, Some cs ->
                                span [] [
                                    sprintf "...with %s additional rows and %i additional variables: " rs notShownColumns |> str
                                    br [] []
                                    yield! cs
                                ]
                                |> Some

                        div [] [
                            table [] [
                                caption [] [ sprintf "A frame: %i x %i" rowCount columnCount |> str ]
                                thead [] [
                                    tr [] [
                                        th [] []
                                        yield! df.ColumnKeys
                                        |> Seq.take (min maxCols columnCount)
                                        |> Seq.map (fun ck -> th [] [ str (ck.ToString()) ])
                                        if maxCols < columnCount then th [] [ str "..." ]
                                    ]
                                    tr [] [
                                        th [] []
                                        yield! df.ColumnTypes
                                        |> Seq.take (min maxCols columnCount)
                                        |> Seq.map (fun ct -> th [] [ ct |> string |> str ])
                                        if maxCols < columnCount then th [] [ str "..." ]
                                    ]
                                ]
                                tbody [] [
                                    yield! df
                                    |> Frame.sliceCols (df.ColumnKeys |> Seq.take (min columnCount maxCols))
                                    |> Frame.take (min maxRows rowCount)
                                    |> Frame.rows
                                    |> Series.observationsAll
                                    |> Seq.map (fun item ->
                                        let def, k, data =
                                            match item with
                                            | k, Some d -> "N/A", k.ToString(), Series.observationsAll d |> Seq.map snd
                                            | k, _ -> "N/A", k.ToString(), df.ColumnKeys |> Seq.map (fun _ -> None)
                                        let toRow v =
                                            td [] [ embed context v ]
                                        let row =
                                            data
                                            |> Seq.map (formatValue def >> toRow)
                                        tr [] [
                                            td [] [ embed context k ]
                                            yield! row
                                            if columnCount > maxCols then td [] [ str "..." ]
                                        ])
                                    if rowCount > maxRows then tr [] [
                                        yield! fun _ -> td [] [ str "..." ]
                                        |> Seq.init ((min columnCount maxCols) + 2)
                                    ]
                                ]
                            ]
                            match summary with
                            | Some s ->
                                div [] [
                                    p [] [ s ]
                                ]
                            | None -> ()
                        ]
                        |> Some
            }
            |> df.Apply
        | _ -> None


    interface IKernelExtension with
        member this.OnLoadAsync kernel =
            Formatter.Register<IFsiFormattable>(Func<FormatContext, IFsiFormattable, TextWriter, bool>(fun (context: FormatContext) (formattable: IFsiFormattable) (writer: TextWriter) ->
                if context.ContentThreshold < 1.0 then false else

                context.ReduceContent(0.2)
                |> ignore

                match getHtml formattable context with
                | Some v -> writer.Write v
                | None -> writer.Write ""

                true
            ), mimeType = "text/html")

            Task.CompletedTask
