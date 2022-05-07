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

type WordStat = { count: int; pos: string }
type AnalysisStat = {
    posFrequencies: IDictionary<string, int>
    topWords: IDictionary<string, WordStat>
    topWordsPerPOS: IDictionary<string, IDictionary<string, int>>
}

let mainDir = Directory.GetParent(__SOURCE_DIRECTORY__).FullName
let magyarlancOutputDir = $"{mainDir}/outputs/analyze/magyarlanc"
let analyzeOutputDir = $"{mainDir}/outputs/stats/magyarlanc"
let wordCloudOutputDir = $"{mainDir}/outputs/word_cloud"
let chartOutputDir = $"{mainDir}/outputs/chart"
let magyarlancPosDistributionChartOutputDir = $"{chartOutputDir}/magyarlanc/pos_distribution"
let magyarlancPosChangesPerGradeChartDir = $"{chartOutputDir}/magyarlanc/pos_changes"
let magyarlancWordCloudOutputDir = $"{wordCloudOutputDir}/magyarlanc"
let mnszDataFilePath = $"{mainDir}/outputs/analyze/mnsz.txt"

let posChartIgnoresPOSes = [| "PART"; "X"; "INTJ"; "SYM"; "AUX"; "PUNCT" |]
let perGradeUsedPOSes = [| "NOUN"; "VERB" |]
let wordCloudPOSes = [| ""; "NOUN"; "VERB"; "ADJ" |]
let topWordsIgnoredPOSes = [| "PUNCT" |]


let createAnalysisStat file =
    let fileStats = file |> File.ReadLines
                         |> Seq.filter(fun k -> k <> String.Empty)
                         |> Seq.map(fun k -> k.Split '\t')
                         |> Seq.map(fun k -> {| word = k.[0]; pos = k.[2] |})
                         |> Seq.toArray

    let posFrequencies = fileStats |> Seq.countBy(fun k -> k.pos)
                                   |> Seq.sortByDescending(fun (_, count) -> count)

    let topWords = fileStats |> Seq.filter(fun k -> not(Array.contains k.pos topWordsIgnoredPOSes))
                             |> Seq.groupBy(fun k -> k.word)
                             |> Seq.map(fun (word, stats) -> (word, {
                                 count = stats |> Seq.length
                                 pos = stats |> Seq.head |> fun k -> k.pos
                             }))

    let topWordsPerPOS = fileStats |> Seq.groupBy(fun k -> k.pos)
                                   |> Seq.map(fun (pos, stats) ->
                                        (pos, stats |> Seq.countBy(fun k -> k.word) |> dict))
    {
        posFrequencies = posFrequencies |> dict
        topWords = topWords |> dict
        topWordsPerPOS = topWordsPerPOS |> dict
    }

let mergeDictionary<'V, 'K when 'K: equality>(dict1: IDictionary<'K, 'V>) (dict2: IDictionary<'K, 'V>) (valueMerger: seq<KeyValuePair<'K,'V>> -> 'V) =
    Seq.append dict1 dict2 |> Seq.groupBy(fun k -> k.Key)
                           |> Seq.map(fun (key, values) -> (key, valueMerger values))

let mergeAnalysisStats stat1 stat2 =
    let mergeTopWordStat accumulator element = {
        count = element.count + accumulator.count
        pos = if accumulator.pos = null then element.pos else accumulator.pos
    }

    let mergeTopWordsPerPOSStat accumulator element = mergeDictionary accumulator element (Seq.sumBy(fun k -> k.Value)) |> dict

    {
        posFrequencies = mergeDictionary stat1.posFrequencies stat2.posFrequencies (Seq.sumBy(fun k -> k.Value)) |> Seq.sortByDescending(fun (_, count) -> count) |> dict
        topWords = mergeDictionary stat1.topWords stat2.topWords (fun k -> k |> Seq.map(fun k -> k.Value)
                                                                             |> Seq.fold mergeTopWordStat { count = 0; pos = null }) |> dict
        topWordsPerPOS = mergeDictionary stat1.topWordsPerPOS stat2.topWordsPerPOS (fun k -> k |> Seq.map(fun k -> k.Value)
                                                                                               |> Seq.fold mergeTopWordsPerPOSStat (dict [])) |> dict
    }

let truncateAnalysisStat stat = {
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
}

let getBookType(fileName: string) = fileName.[fileName.IndexOf '_' + 1 ..]
let getBookGrade(fileName: string) = fileName.[.. (fileName.IndexOf '_' - 1)]
let writeChart chartData = Process.Start("python", [| "genChart.py"; JsonSerializer.Serialize(chartData) |]).WaitForExit()

let writeWordCloud(topWords: IDictionary<string, WordStat>) (baseFilePath: string) (pos: string) =
    let filePostfix = if pos = "" then "" else $"_{pos}"
    let filteredWords = if pos = "" then topWords
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

let writePosChart filePath posFrequencies = writeChart {|
    filePath = filePath
    chartType = "pos_counts"
    legendTitle = "Szófaj"
    data = posFrequencies
|}

let writeDottedLineChart filePath perGradeStats = writeChart {|
    filePath = filePath
    chartType = "per_grade_pos_counts"
    legendTitle = "Szófaj"
    data = perGradeStats
|}

let writeAnalysisStat fileName stats =
    let statJsonSettings = JsonSerializerOptions(WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping)
    let wordCloudBaseFilePath = $"{magyarlancWordCloudOutputDir}/{fileName}"

    File.WriteAllText($"{analyzeOutputDir}/{fileName}.json", JsonSerializer.Serialize(stats |> truncateAnalysisStat, statJsonSettings))
    wordCloudPOSes |> Seq.iter(writeWordCloud stats.topWords wordCloudBaseFilePath)
    stats.posFrequencies |> Seq.filter(fun k -> not(Array.contains k.Key posChartIgnoresPOSes))
                         |> Seq.map(fun k -> (k.Key, k.Value))
                         |> dict
                         |> writePosChart $"{magyarlancPosDistributionChartOutputDir}/{fileName}.png"


let beginTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
let emptyStats = { posFrequencies = dict([]); topWords = dict([]); topWordsPerPOS = dict([]) }

let perBookStats = Directory.GetFiles magyarlancOutputDir
                 |> PSeq.map(fun k -> (Path.GetFileNameWithoutExtension k, createAnalysisStat k))
                 |> PSeq.toArray

let perBookTypeStats = perBookStats |> Seq.groupBy(fun (fileName, _) -> getBookType fileName)
                                    |> dict

let szgyStats = perBookTypeStats.["szgy"] |> Seq.map(fun (_, stats) -> stats)
                                          |> Seq.fold mergeAnalysisStats emptyStats

let tkStats = perBookTypeStats.["tk"] |> Seq.map(fun (_, stats) -> stats)
                                      |> Seq.fold mergeAnalysisStats emptyStats

let mergedStats = mergeAnalysisStats szgyStats tkStats


Directory.CreateDirectory analyzeOutputDir
Directory.CreateDirectory magyarlancWordCloudOutputDir
Directory.CreateDirectory magyarlancPosDistributionChartOutputDir
Directory.CreateDirectory magyarlancPosChangesPerGradeChartDir

perBookStats |> PSeq.iter(fun (fileName, stats) -> writeAnalysisStat fileName stats)
perBookTypeStats |> PSeq.iter(fun k ->
    let posFrequenciesLookup = k.Value |> Seq.map(fun (fileName, stats) -> (stats.posFrequencies, getBookGrade fileName))
                                       |> Seq.sortBy(fun (_, grade) -> int grade)
                                       |> dict

    perGradeUsedPOSes |> Seq.map(fun pos -> (pos, posFrequenciesLookup |> Seq.map(fun n -> (n.Value, n.Key.[pos])) |> dict))
                      |> dict
                      |> writeDottedLineChart $"{magyarlancPosChangesPerGradeChartDir}/{k.Key}.png"
)

writeAnalysisStat "szgy_merged" szgyStats
writeAnalysisStat "tk_merged" tkStats
writeAnalysisStat "merged" mergedStats


if File.Exists mnszDataFilePath then
    let noGibberishWordFilter = Regex("^[a-zA-Z0-9]*$")
    let mnszWordCloudOutputDir = $"{wordCloudOutputDir}/mnsz"
    let posTransformer = function
        | "N" -> "NOUN"
        | "V" -> "VERB"
        | "A" -> "ADJ"
        | k -> k

    mnszDataFilePath |> File.ReadLines
                     |> Seq.map(fun k -> k.Split '\t')
                     |> Seq.filter(fun k -> noGibberishWordFilter.IsMatch k.[0])
                     |> Seq.map(fun k -> (k.[0], { count = int k.[7]; pos = posTransformer k.[3] }))
                     |> dict
                     |> fun topWords -> wordCloudPOSes |> Seq.iter(writeWordCloud topWords mnszWordCloudOutputDir)


let endTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
printfn "All done in %dms" (endTime - beginTime)