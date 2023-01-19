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

type WordStat = IDictionary<string, int>
type POSStat = {
    frequency: int
    averageWordLength: float
    mostFrequentWords: IDictionary<string, int>
    longestWords: IDictionary<string, int>
}

type WordCountStat = {
    total: int
    unique: int
}

type AnalysisStat = {
    wordCounts: WordCountStat
    wordFrequencies: IDictionary<string, WordStat>
    wordLengths: IDictionary<string, int>
    posStats: IDictionary<string, POSStat>
}

let mainDir = Directory.GetParent(__SOURCE_DIRECTORY__).FullName
let magyarlancOutputDir = $"{mainDir}/outputs/analyze/magyarlanc"
let statsOutputDir = $"{mainDir}/outputs/stats"
let magyarlancStatsOutputDir = $"{statsOutputDir}/magyarlanc"
let wordCloudOutputDir = $"{mainDir}/outputs/word_cloud"
let chartOutputDir = $"{mainDir}/outputs/chart"
let magyarlancPosDistributionChartOutputDir = $"{chartOutputDir}/magyarlanc/pos_distribution"
let magyarlancPosChangesPerGradeChartDir = $"{chartOutputDir}/magyarlanc/pos_changes"
let magyarlancWordCloudOutputDir = $"{wordCloudOutputDir}/magyarlanc"
let mnszDataFilePath = $"{mainDir}/outputs/analyze/hnc-1.3-wordfreq.txt"

let statJsonSettings = JsonSerializerOptions(WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping)

let CHART_PIE = "pie"
let CHART_DOTTED_LINE = "dotted_line"
let posChartIgnoredMagyarlancPOSes = [| "PART"; "X"; "INTJ"; "SYM"; "AUX"; "PUNCT" |]
let posChartIgnoredMNSZPOSes = [| "UNKNOWNTAG"; "UNKNOWN"; "S"; "UNKNOWNTAG-EMPTY"; "ELO"; "MIB"; "NU"; "Int"; "DATUM"; "Num Num"; "NU NU"; "Pre Pre"; "A A"; "A MIB" |]
let perGradeUsedPOSes = [| "NOUN"; "VERB"; "PROPN" |]
let wordCloudPOSes = [| ""; "NOUN"; "VERB"; "ADJ" |]
let magyarlancAnalysisIgnoredPOSes = [| "PUNCT"; ""; "SYM"; "X" |]


let parseMagyarlancWordFrequencies file =
    let calculatePOSFrequencies(args: seq<string[]>) = args |> Seq.countBy(fun k -> k.[2]) |> dict

    file |> File.ReadLines
         |> Seq.filter(fun k -> k <> String.Empty)
         |> Seq.map(fun k -> k.Split '\t')
         |> Seq.filter(fun k -> not(Array.contains k.[2] magyarlancAnalysisIgnoredPOSes))
         |> Seq.groupBy(fun k -> k.[0])
         |> Seq.map(fun (word, args) -> {| word = word; posFrequencies = calculatePOSFrequencies args |})
         |> Seq.toArray

let parseMNSZWordFrequencies file =
    let noGibberishWordFilter = Regex("^[a-zA-Z0-9]*$")
    let posTransformer = function
        | "N" -> "NOUN"
        | "V" -> "VERB"
        | "A" -> "ADJ"
        | k -> k

    let calculatePOSFrequencies(args: seq<string[]>) =
        args |> Seq.groupBy(fun k -> posTransformer k.[3])
             |> Seq.map(fun (pos, statsForPOS) -> (pos, statsForPOS |> Seq.sumBy(fun k -> int k.[7])))
             |> dict

    file |> File.ReadLines
         |> Seq.map(fun k -> k.Split '\t')
         |> Seq.filter(fun k -> noGibberishWordFilter.IsMatch k.[0])
         |> Seq.groupBy(fun k -> k.[0])
         |> Seq.map(fun (word, args) -> {| word = word; posFrequencies = calculatePOSFrequencies args |})
         |> Seq.toArray


let createWordCountStat(wordFrequencies: IDictionary<string, WordStat>) = {
    total = wordFrequencies |> Seq.collect(fun k -> k.Value.Values) |> Seq.sum
    unique = wordFrequencies.Count
}

let createAnalysisStat(rawWordFrequencies: {| word: string; posFrequencies: IDictionary<string, int> |}[]) =
    let createPOSStat pos =
        let wordsToPOSFrequency = rawWordFrequencies |> Seq.filter(fun k -> k.posFrequencies.ContainsKey pos)
                                                     |> Seq.map(fun k -> (k.word, k.posFrequencies.[pos]))
                                                     |> dict

        let mostFrequentWords = wordsToPOSFrequency |> Seq.map(|KeyValue|)
                                                    |> Seq.sortByDescending(fun (_, freq) -> freq)

        let longestWords = wordsToPOSFrequency.Keys |> Seq.map(fun k -> (k, k.Length))
                                                    |> Seq.sortByDescending(fun (_, length) -> length)
        {
            frequency = wordsToPOSFrequency |> Seq.sumBy(fun (KeyValue(_, freq)) -> freq)
            averageWordLength = wordsToPOSFrequency |> Seq.averageBy(fun (KeyValue(word, _)) -> float word.Length)
            mostFrequentWords = mostFrequentWords |> dict
            longestWords = longestWords |> dict
        }

    let posStats = rawWordFrequencies |> Seq.collect(fun k -> k.posFrequencies.Keys)
                                      |> Seq.distinct
                                      |> Seq.map(fun pos -> (pos, createPOSStat pos))
                                      |> Seq.sortByDescending(fun (_, stats) -> stats.frequency)

    let wordFrequencies = rawWordFrequencies |> Seq.map(fun k -> (k.word, k.posFrequencies)) |> dict

    {
        wordCounts = createWordCountStat wordFrequencies
        wordFrequencies = wordFrequencies
        wordLengths = rawWordFrequencies |> Seq.map(fun k -> (k.word, k.word.Length)) |> dict
        posStats = posStats |> dict
    }


let mergeDictionary<'V, 'K when 'K: equality>(valueMerger: seq<'V> -> 'V) (dict1: IDictionary<'K, 'V>) (dict2: IDictionary<'K, 'V>) =
    Seq.append dict1 dict2 |> Seq.groupBy(fun (KeyValue(key, _)) -> key)
                           |> Seq.map(fun (key, items) -> (key, valueMerger (items |> Seq.map(fun (KeyValue(_, value)) -> value))))
                           |> dict

let mergeAnalysisStats s1 s2 =
    let mergeWordFrequency = Seq.fold(mergeDictionary Seq.sum) (dict([]))
    let mergePOSStat k = {
        frequency = k |> Seq.sumBy(fun k -> k.frequency)
        averageWordLength = k |> Seq.averageBy(fun k -> k.averageWordLength)
        mostFrequentWords = k |> Seq.map(fun k -> k.mostFrequentWords)
                              |> Seq.fold(mergeDictionary Seq.sum) (dict [])
        longestWords = k |> Seq.map(fun k -> k.longestWords)
                         |> Seq.fold(mergeDictionary Seq.max) (dict [])
    }

    let mergedWordFrequencies = mergeDictionary mergeWordFrequency s1.wordFrequencies s2.wordFrequencies

    {
        wordCounts = createWordCountStat mergedWordFrequencies
        wordFrequencies = mergedWordFrequencies
        wordLengths = mergeDictionary Seq.head s1.wordLengths s2.wordLengths
        posStats = mergeDictionary mergePOSStat s1.posStats s2.posStats
    }

let truncateAnalysisStat stat =
    let truncatedWordFrequencies = stat.wordFrequencies |> Seq.sortByDescending(fun (KeyValue(_, stat)) -> stat.Values |> Seq.sum)
                                                        |> Seq.truncate 100
                                                        |> Seq.map(|KeyValue|)

    let truncatedWordLengths = stat.wordLengths |> Seq.sortByDescending(fun (KeyValue(_, length)) -> length)
                                                |> Seq.truncate 50
                                                |> Seq.map(|KeyValue|)

    let truncateMostFrequentWords(mostFrequentWords: IDictionary<string, int>) =
        mostFrequentWords |> Seq.sortByDescending(fun (KeyValue(_, freq)) -> freq)
                          |> Seq.truncate 5
                          |> Seq.map(|KeyValue|)
                          |> dict

    let trucateLongestWords(longestWords: IDictionary<string, int>) =
        longestWords |> Seq.sortByDescending(fun (KeyValue(_, length)) -> length)
                     |> Seq.truncate 5
                     |> Seq.map(|KeyValue|)
                     |> dict

    let truncatedPOSStats = stat.posStats |> Seq.map(fun (KeyValue(pos, stat)) -> (pos, {
                                                        frequency = stat.frequency
                                                        averageWordLength = stat.averageWordLength
                                                        longestWords = trucateLongestWords stat.longestWords
                                                        mostFrequentWords = truncateMostFrequentWords stat.mostFrequentWords
                                                    }))
                                          |> Seq.sortByDescending(fun (_, stats) -> stats.frequency)
    {
        wordCounts = stat.wordCounts
        wordFrequencies = truncatedWordFrequencies |> dict
        wordLengths = truncatedWordLengths |> dict
        posStats = truncatedPOSStats |> dict
    }

let parseBookType(fileName: string) = fileName.[fileName.IndexOf '_' + 1 ..]
let parseBookGrade(fileName: string) = fileName.[.. (fileName.IndexOf '_' - 1)]

let writeWordCloud(wordFrequencies: IDictionary<string, WordStat>) (baseFilePath: string) (pos: string) =
    let wordFrequencyCalculator: IDictionary<string, int> -> int =
        if pos = ""
        then fun k -> k |> Seq.sumBy(fun (KeyValue(_, freq)) -> freq)
        else fun k -> if k.ContainsKey pos then k.[pos] else 0

    let topWordsToWrite = wordFrequencies |> Seq.map(fun (KeyValue(word, stat)) -> (word, wordFrequencyCalculator stat))
                                          |> Seq.sortByDescending(fun (_, freq) -> freq)
                                          |> Seq.truncate 500
                                          |> Seq.map(WordCloudEntry)

    let wordCloud = WordCloudInput(topWordsToWrite, Width = 1920, Height = 1080, MinFontSize = 1, MaxFontSize = 84)
    use engine = new SkGraphicEngine(LogSizer(wordCloud), wordCloud)
    let generator = WordCloudGenerator<SKBitmap>(wordCloud, engine, SpiralLayout(wordCloud), RandomColorizer())

    use bitmap = new SKBitmap(wordCloud.Width, wordCloud.Height)
    use canvas = new SKCanvas(bitmap)
    canvas.Clear(SKColors.White)
    canvas.DrawBitmap(generator.Draw(), SKPoint(0F, 0F))

    let filePostfix = if pos = "" then "" else $"_{pos}"
    use image = bitmap.Encode(SKEncodedImageFormat.Jpeg, 100)
    image.SaveTo(File.Open($"{baseFilePath}{filePostfix}.jpg", FileMode.Create))

let writeChart filePath chartType legendTitle data =
    let dataToPass = {|
        filePath = filePath
        chartType = chartType
        legendTitle = legendTitle
        data = data
    |}

    Process.Start("python", [| "genChart.py"; JsonSerializer.Serialize dataToPass |]).WaitForExit()

let writeAnalysisStat fileName stats =
    let wordCloudBaseFilePath = $"{magyarlancWordCloudOutputDir}/{fileName}"

    File.WriteAllText($"{magyarlancStatsOutputDir}/{fileName}.json", JsonSerializer.Serialize(stats |> truncateAnalysisStat, statJsonSettings))
    wordCloudPOSes |> Seq.iter(writeWordCloud stats.wordFrequencies wordCloudBaseFilePath)
    stats.posStats |> Seq.filter(fun (KeyValue(pos, _)) -> not(Array.contains pos posChartIgnoredMagyarlancPOSes))
                   |> Seq.map(fun (KeyValue(pos, stat)) -> (pos, stat.frequency))
                   |> Seq.sortByDescending(fun (_, freq) -> freq)
                   |> dict
                   |> writeChart $"{magyarlancPosDistributionChartOutputDir}/{fileName}.png" CHART_PIE "Szófaj"


let beginTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
let identityStat = { wordCounts = { total = 0; unique = 0 }; wordFrequencies = dict([]); wordLengths = dict([]); posStats = dict([]) }

let perBookStats = Directory.GetFiles magyarlancOutputDir
                 |> PSeq.map(fun k -> (Path.GetFileNameWithoutExtension k, k |> parseMagyarlancWordFrequencies |> createAnalysisStat))
                 |> PSeq.toArray

let perBookTypeStats = perBookStats |> Seq.groupBy(fun (fileName, _) -> parseBookType fileName)
                                    |> dict

let szgyStats = perBookTypeStats.["szgy"] |> Seq.map(fun (_, stats) -> stats)
                                          |> Seq.fold mergeAnalysisStats identityStat

let tkStats = perBookTypeStats.["tk"] |> Seq.map(fun (_, stats) -> stats)
                                      |> Seq.fold mergeAnalysisStats identityStat

let mergedStats = mergeAnalysisStats szgyStats tkStats

Directory.CreateDirectory magyarlancStatsOutputDir
Directory.CreateDirectory magyarlancWordCloudOutputDir
Directory.CreateDirectory magyarlancPosDistributionChartOutputDir
Directory.CreateDirectory magyarlancPosChangesPerGradeChartDir

perBookStats |> PSeq.iter(fun (fileName, stats) -> writeAnalysisStat fileName stats)
perBookTypeStats |> PSeq.iter(fun (KeyValue(bookType, bookTypeStats)) ->
    let posStatLookup = bookTypeStats |> Seq.map(fun (fileName, stats) -> (stats.posStats, parseBookGrade fileName))
                                      |> Seq.sortBy(fun (_, grade) -> int grade)
                                      |> dict

    perGradeUsedPOSes |> Seq.map(fun pos -> (pos, posStatLookup |> Seq.map(fun (KeyValue(posStat, grade)) -> (grade, posStat.[pos].frequency)) |> dict))
                      |> dict
                      |> writeChart $"{magyarlancPosChangesPerGradeChartDir}/{bookType}.png" CHART_DOTTED_LINE "Szófaj"
)

writeAnalysisStat "szgy_merged" szgyStats
writeAnalysisStat "tk_merged" tkStats
writeAnalysisStat "merged" mergedStats


if File.Exists mnszDataFilePath then
    let mnszAnalysisStat = mnszDataFilePath |> parseMNSZWordFrequencies |> createAnalysisStat

    File.WriteAllText($"{statsOutputDir}/mnsz.json", JsonSerializer.Serialize(mnszAnalysisStat |> truncateAnalysisStat, statJsonSettings))
    wordCloudPOSes |> Seq.iter(writeWordCloud mnszAnalysisStat.wordFrequencies $"{wordCloudOutputDir}/mnsz")

    mnszAnalysisStat.posStats |> Seq.filter(fun (KeyValue(pos, _)) -> not(Array.contains pos posChartIgnoredMNSZPOSes))
                              |> Seq.map(fun (KeyValue(pos, stat)) -> (pos, stat.frequency))
                              |> Seq.sortByDescending(fun (_, freq) -> freq)
                              |> dict
                              |> writeChart $"{chartOutputDir}/mnsz_pos_distribution.png" CHART_PIE "Szófaj"

let endTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
printfn "All done in %dms" (endTime - beginTime)