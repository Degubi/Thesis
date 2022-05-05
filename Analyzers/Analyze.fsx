#r "nuget: KnowledgePicker.WordCloud"
#r "nuget: FSharp.Collections.ParallelSeq"

open System
open System.Collections.Generic
open System.Diagnostics
open System.IO
open System.Text.Encodings.Web
open System.Text.Json
open System.Text.RegularExpressions
open FSharp.Collections.ParallelSeq
open KnowledgePicker.WordCloud
open KnowledgePicker.WordCloud.Coloring
open KnowledgePicker.WordCloud.Drawing
open KnowledgePicker.WordCloud.Layouts
open KnowledgePicker.WordCloud.Primitives
open KnowledgePicker.WordCloud.Sizers
open SkiaSharp

type AnalysisStat = {|
    posFrequencies: IDictionary<string, int>
    topWords: IDictionary<string, {| count: int; pos: string |}>
    topWordsPerPOS: IDictionary<string, IDictionary<string, int>>
|}

let mainDir = Directory.GetParent(__SOURCE_DIRECTORY__).FullName
let statJsonSettings = JsonSerializerOptions(WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping)
let inputFiles = Directory.GetFiles $"{mainDir}/outputs/analyze/magyarlanc"
let analyzeOutputDir = $"{mainDir}/outputs/stats/magyarlanc"
let wordCloudOutputDir = $"{mainDir}/outputs/word_cloud"
let chartOutputDir = $"{mainDir}/outputs/chart"
let magyarlancPosDistributionChartOutputDir = chartOutputDir + "/magyarlanc/pos_distr"
let magyarlancWordCloudOutputDir = wordCloudOutputDir + "/magyarlanc"
let mnszFilePath = $"{mainDir}/outputs/analyze/mnsz.txt"
let posChartIgnoresPOSes = [| "PART"; "X"; "INTJ"; "SYM"; "AUX"; "PUNCT" |]
let topWordsIgnoredPOSes = [| "PUNCT" |]


let mergeDictionary<'V, 'K when 'K: equality>(dict1: IDictionary<'K, 'V>) (dict2: IDictionary<'K, 'V>) (valueMerger: seq<KeyValuePair<'K,'V>> -> 'V) =
    Seq.append dict1 dict2 |> Seq.groupBy(fun k -> k.Key)
                           |> Seq.map(fun (key, values) -> (key, valueMerger values))

let mergeAnalysisStats(stat1: AnalysisStat) (stat2: AnalysisStat) =
    let mergeTopWordStat(accumulator: {| count: int; pos: string |}) (element: {| count: int; pos: string |}) = {|
        count = element.count + accumulator.count
        pos = if accumulator.pos = null then element.pos else accumulator.pos
    |}

    let mergeTopWordsPerPOSStat accumulator element = mergeDictionary accumulator element (Seq.sumBy(fun k -> k.Value)) |> dict

    {|
        posFrequencies = mergeDictionary stat1.posFrequencies stat2.posFrequencies (Seq.sumBy(fun k -> k.Value)) |> Seq.sortByDescending(fun (_, count) -> count) |> dict
        topWords = mergeDictionary stat1.topWords stat2.topWords (fun k -> k |> Seq.map(fun k -> k.Value)
                                                                             |> Seq.fold mergeTopWordStat {| count = 0; pos = null |}) |> dict
        topWordsPerPOS = mergeDictionary stat1.topWordsPerPOS stat2.topWordsPerPOS (fun k -> k |> Seq.map(fun k -> k.Value)
                                                                                               |> Seq.fold mergeTopWordsPerPOSStat (dict [])) |> dict
    |}

let createAnalysisStat file =
    let fileStats = file |> File.ReadLines
                          |> Seq.filter(fun k -> k <> String.Empty)
                          |> Seq.map(fun k -> k.Split '\t')
                          |> Seq.map(fun k -> {| word = k.[0]; pos = k.[2] |})
                          |> Seq.toArray

    let topWords = fileStats |> Seq.filter(fun k -> not(Array.contains k.pos topWordsIgnoredPOSes))
                             |> Seq.groupBy(fun k -> k.word)
                             |> Seq.map(fun (word, stats) -> (word, {|
                                 count = stats |> Seq.length
                                 pos = stats |> Seq.head |> fun k -> k.pos
                             |}))

    let topWordsPerPOS = fileStats |> Seq.groupBy(fun k -> k.pos)
                                   |> Seq.map(fun (pos, stats) ->
                                        (pos, stats |> Seq.countBy(fun k -> k.word) |> dict))
    {|
        posFrequencies = fileStats |> Seq.countBy(fun k -> k.pos) |> Seq.sortByDescending(fun (_, count) -> count) |> dict
        topWords = topWords |> dict
        topWordsPerPOS = topWordsPerPOS |> dict
    |}

let truncateAnalysisStat(stat: AnalysisStat) = {|
    posFrequencies = stat.posFrequencies
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

let writeWordCloud(topWords: IDictionary<string, {| count: int; pos: string |}>) (pos: string) (baseFilePath: string) =
    let filePostfix = if pos = null then "" else $"_{pos}"
    let filteredWords = if pos = null then topWords
                        else topWords |> Seq.filter(fun k -> k.Value.pos = pos) |> Seq.map(fun k -> (k.Key, k.Value)) |> dict

    let topWordsToWrite = filteredWords |> Seq.sortByDescending(fun k -> k.Value.count)
                                        |> Seq.truncate 500
                                        |> Seq.map(fun k -> WordCloudEntry(k.Key, k.Value.count))

    let wordCloud = WordCloudInput(topWordsToWrite, Width = 1920, Height = 1080, MinFontSize = 1, MaxFontSize = 84)
    use engine = new SkGraphicEngine(LogSizer(wordCloud), wordCloud)
    let generator = WordCloudGenerator<SKBitmap>(wordCloud, engine, SpiralLayout(wordCloud), RandomColorizer())

    use bitmap = new SKBitmap(wordCloud.Width, wordCloud.Height)
    use canvas = new SKCanvas(bitmap)
    canvas.Clear(SKColors.White)
    canvas.DrawBitmap(generator.Draw(), SKPoint(0F, 0F))

    use image = bitmap.Encode(SKEncodedImageFormat.Jpeg, 100)
    image.SaveTo(File.Open($"{baseFilePath}{filePostfix}.jpg", FileMode.Create))

let writeWordClouds(topWords: IDictionary<string, {| count: int; pos: string |}>) (baseFilePath: string) =
    writeWordCloud topWords null baseFilePath
    writeWordCloud topWords "NOUN" baseFilePath
    writeWordCloud topWords "VERB" baseFilePath
    writeWordCloud topWords "ADJ" baseFilePath

let writePosChart(posCounts: IDictionary<string, int>) (filePath: string) =
    let filteredPosCounts = posCounts |> Seq.filter(fun k -> not(Array.contains k.Key posChartIgnoresPOSes))
                                      |> Seq.map(fun k -> (k.Key, k.Value))
                                      |> dict
    let chartData = {|
        filePath = filePath
        title = "Szófaj"
        labels = filteredPosCounts.Keys
        values = filteredPosCounts.Values
    |}

    Process.Start("python", [| "genChart.py"; JsonSerializer.Serialize(chartData) |])
           .WaitForExit()

let writeAnalysisStat(fileName: string) (stats: AnalysisStat) =
    File.WriteAllText($"{analyzeOutputDir}/{fileName}.json", JsonSerializer.Serialize(stats |> truncateAnalysisStat, statJsonSettings))
    writeWordClouds stats.topWords $"{magyarlancWordCloudOutputDir}/{fileName}"
    writePosChart stats.posFrequencies $"{magyarlancPosDistributionChartOutputDir}/{fileName}.png"


let emptyStats = {| posFrequencies = dict([]); topWords = dict([]); topWordsPerPOS = dict([]) |}
let beginTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()

Directory.CreateDirectory analyzeOutputDir
Directory.CreateDirectory magyarlancWordCloudOutputDir
Directory.CreateDirectory magyarlancPosDistributionChartOutputDir

let perBookStats = inputFiles |> PSeq.map(fun k -> (Path.GetFileNameWithoutExtension k, createAnalysisStat k))
                              |> PSeq.toArray

let szgyStats = perBookStats |> Seq.filter(fun (fileName, _) -> fileName.EndsWith "_szgy")
                             |> Seq.map(fun (_, stats) -> stats)
                             |> Seq.fold mergeAnalysisStats emptyStats

let tkStats = perBookStats |> Seq.filter(fun (fileName, _) -> fileName.EndsWith "_tk")
                           |> Seq.map(fun (_, stats) -> stats)
                           |> Seq.fold mergeAnalysisStats emptyStats

let mergedStats = mergeAnalysisStats szgyStats tkStats

perBookStats |> PSeq.iter(fun (fileName, stats) -> writeAnalysisStat fileName stats)

writeAnalysisStat "szgy_merged" szgyStats
writeAnalysisStat "tk_merged" tkStats
writeAnalysisStat "merged" mergedStats

if File.Exists mnszFilePath then
    let noGibberishWordFilter = Regex("^[a-zA-Z0-9]*$")
    let posTransformer = function
        | "N" -> "NOUN"
        | "V" -> "VERB"
        | "A" -> "ADJ"
        | k -> k

    let mnszWords = File.ReadLines mnszFilePath |> Seq.map(fun k -> k.Split '\t')
                                                |> Seq.filter(fun k -> noGibberishWordFilter.IsMatch k.[0])
                                                |> Seq.map(fun k -> (k.[0], {| count = int k.[7]; pos = posTransformer k.[3] |}))
                                                |> dict

    writeWordClouds mnszWords $"{wordCloudOutputDir}/mnsz"


let endTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
printfn "All done in %dms" (endTime - beginTime)