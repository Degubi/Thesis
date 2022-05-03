#r "nuget: KnowledgePicker.WordCloud"
#r "nuget: FSharp.Collections.ParallelSeq"

open System
open System.Collections.Generic
open System.IO
open System.Text.Encodings.Web
open System.Text.Json
open FSharp.Collections.ParallelSeq
open KnowledgePicker.WordCloud
open KnowledgePicker.WordCloud.Coloring
open KnowledgePicker.WordCloud.Drawing
open KnowledgePicker.WordCloud.Layouts
open KnowledgePicker.WordCloud.Primitives
open KnowledgePicker.WordCloud.Sizers
open SkiaSharp

type AnalysisStat = {|
    posCounts: IDictionary<string, int>
    topWords: IDictionary<string, {| count: int; pos: string |}>
    topWordsPerPOS: IDictionary<string, IDictionary<string, int>>
|}

let ignoredPOSes = [| "PUNCT" |]

let createAnalysisStat lines =
    let fileStats = lines |> Seq.filter(fun k -> k <> String.Empty)
                          |> Seq.map(fun k -> k.Split '\t')
                          |> Seq.map(fun k -> {| word = k.[0]; pos = k.[2] |})
                          |> Seq.toArray

    let topWords = fileStats |> Seq.filter(fun k -> not(Array.contains k.pos ignoredPOSes))
                             |> Seq.groupBy(fun k -> k.word)
                             |> Seq.map(fun (word, stats) -> (word, {|
                                 count = stats |> Seq.length
                                 pos = stats |> Seq.head |> fun k -> k.pos
                             |}))

    let topWordsPerPOS = fileStats |> Seq.groupBy(fun k -> k.pos)
                                   |> Seq.map(fun (pos, stats) ->
                                        (pos, stats |> Seq.countBy(fun k -> k.word) |> dict))
    {|
        posCounts = fileStats |> Seq.countBy(fun k -> k.pos) |> dict
        topWords = topWords |> dict
        topWordsPerPOS = topWordsPerPOS |> dict
    |}

let truncateStat(stat: AnalysisStat) = {|
    posCounts = stat.posCounts
    topWords = stat.topWords |> Seq.sortByDescending(fun k -> k.Value.count)
                             |> Seq.truncate 100
                             |> Seq.map(fun k -> (k.Key, k.Value))
                             |> dict
    topWordsPerPOS = stat.topWordsPerPOS |> Seq.map(fun k -> (k.Key, k.Value |> Seq.sortByDescending(fun m -> m.Value)
                                                                             |> Seq.truncate 5
                                                                             |> Seq.map(fun k -> (k.Key, k.Value))
                                                                             |> dict))
                                         |> dict
|}


let writeWordCloud(topWords: IDictionary<string, {| count: int; pos: string |}>) (posEs: string[]) (filePath: string) =
    let topWordsToWrite = if posEs.Length = 0 then topWords
                          else topWords |> Seq.filter(fun k -> Array.contains k.Value.pos posEs) |> Seq.map(fun k -> (k.Key, k.Value)) |> dict

    let wordCloud = WordCloudInput(topWordsToWrite |> Seq.map(fun k -> WordCloudEntry(k.Key, k.Value.count)),
        Width = 1920,
        Height = 1080,
        MinFontSize = 1,
        MaxFontSize = 84
    )

    use engine = new SkGraphicEngine(LogSizer(wordCloud), wordCloud)
    let generator = WordCloudGenerator<SKBitmap>(wordCloud, engine, SpiralLayout(wordCloud), RandomColorizer())

    use bitmap = new SKBitmap(wordCloud.Width, wordCloud.Height)
    use canvas = new SKCanvas(bitmap)
    canvas.Clear(SKColors.White)
    canvas.DrawBitmap(generator.Draw(), SKPoint(0F, 0F))

    use image = bitmap.Encode(SKEncodedImageFormat.Jpeg, 100)
    image.SaveTo(File.Open(filePath, FileMode.Open))


let mainDir = Directory.GetParent(__SOURCE_DIRECTORY__).FullName
let jsonSettings = JsonSerializerOptions(WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping)
let inputFiles = Directory.GetFiles $"{mainDir}/outputs/analyze/magyarlanc"
let analyzeOutputDir = $"{mainDir}/outputs/stats/magyarlanc"
let wordCloudOutputDir = $"{mainDir}/outputs/word_cloud/magyarlanc"
let customWordcloudPOSes = [| "NOUN"; "VERB"; "ADJ"; "ADP"; "PROPN"; "N"; "V"; "A"; "NU" |]
let beginTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()

Directory.CreateDirectory analyzeOutputDir
Directory.CreateDirectory wordCloudOutputDir

inputFiles |> PSeq.map(fun k -> (Path.GetFileNameWithoutExtension k, k |> File.ReadLines |> createAnalysisStat))
           |> PSeq.iter(fun (fileName, stats) ->
                File.WriteAllText($"{analyzeOutputDir}/{fileName}.json", JsonSerializer.Serialize(stats |> truncateStat, jsonSettings))
                writeWordCloud stats.topWords Array.empty $"{wordCloudOutputDir}/{fileName}_all.jpg"
                writeWordCloud stats.topWords customWordcloudPOSes $"{wordCloudOutputDir}/{fileName}_filtered.jpg"
           )

let mergedStats = inputFiles |> Seq.map(File.ReadLines)
                             |> Seq.concat
                             |> createAnalysisStat

File.WriteAllText($"{analyzeOutputDir}/merged.json", JsonSerializer.Serialize(mergedStats |> truncateStat, jsonSettings))
writeWordCloud mergedStats.topWords Array.empty $"{wordCloudOutputDir}/merged_all.jpg"
writeWordCloud mergedStats.topWords customWordcloudPOSes $"{wordCloudOutputDir}/merged_filtered.jpg"

let mnszTopWords = File.ReadLines $"{mainDir}/outputs/analyze/mnsz.txt" |> Seq.map(fun k -> k.Split '\t')
                                                                        |> Seq.map(fun k -> (k.[0], {| count = int k.[2]; pos = k.[1] |}))
                                                                        |> dict

writeWordCloud mnszTopWords Array.empty $"{wordCloudOutputDir}/mnsz_all.jpg"
writeWordCloud mnszTopWords customWordcloudPOSes $"{wordCloudOutputDir}/mnsz_filtered.jpg"

let endTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
printfn "All done in %dms" (endTime - beginTime)