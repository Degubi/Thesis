#r "nuget: WordCloud"
#r "nuget: FSharp.Collections.ParallelSeq"

open System
open System.Collections.Generic
open System.IO
open System.Text.Encodings.Web
open System.Text.Json
open FSharp.Collections.ParallelSeq
open WordCloud

let ignoredPOSes = [| "PUNCT" |]

let createAnalysisStat lines =
    let fileStats = lines |> Seq.filter(fun k -> k <> String.Empty)
                          |> Seq.map(fun k -> k.Split '\t')
                          |> Seq.map(fun k -> {| word = k.[0]; pos = k.[2] |})
                          |> Seq.toArray

    let topWords = fileStats |> Seq.filter(fun k -> not(Array.contains k.pos ignoredPOSes))
                             |> Seq.groupBy(fun k -> k.word)
                             |> Seq.map(fun (word, stats) -> (word, {| count = stats |> Seq.length; pos = stats |> Seq.head |> fun k -> k.pos |}))
                             |> Seq.sortByDescending(fun (_, k) -> k.count)
                             |> Seq.truncate 100

    let topWordsPerPOS = fileStats |> Seq.groupBy(fun k -> k.pos)
                                   |> Seq.map(fun (pos, stats) -> (pos, stats |> Seq.countBy(fun k -> k.word)
                                                                              |> Seq.sortByDescending(fun (_, frequency) -> frequency)
                                                                              |> Seq.truncate 5
                                                                              |> dict))
    {|
        posCounts = fileStats |> Seq.countBy(fun k -> k.pos) |> dict
        topWords = topWords |> dict
        topWordsPerPOS = topWordsPerPOS |> dict
    |}

let writeWordCloud(topWords: IDictionary<string, {| count: int; pos: string |}>) (filePath: string) =
    WordCloud(1024, 768, true).Draw(topWords.Keys |> List, topWords |> Seq.map(fun k -> k.Value.count) |> List).Save(filePath)


let mainDir = Directory.GetParent(__SOURCE_DIRECTORY__).FullName
let jsonSettings = JsonSerializerOptions(WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping)
let inputFiles = Directory.GetFiles $"{mainDir}/outputs/analyze/magyarlanc"
let analyzeOutputDir = $"{mainDir}/outputs/stats/magyarlanc"
let wordCloudOutputDir = $"{mainDir}/outputs/word_cloud/magyarlanc"
let beginTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()

Directory.CreateDirectory analyzeOutputDir
Directory.CreateDirectory wordCloudOutputDir

let perBookStats = inputFiles |> Seq.map(fun k -> (Path.GetFileNameWithoutExtension k, k |> File.ReadLines |> createAnalysisStat))
                              |> Seq.toArray

perBookStats |> Seq.iter(fun (fileName, stats) -> File.WriteAllText($"{analyzeOutputDir}/{fileName}.json", JsonSerializer.Serialize(stats, jsonSettings)))
perBookStats |> PSeq.iter(fun (fileName, stats) -> writeWordCloud stats.topWords $"{wordCloudOutputDir}/{fileName}.jpg")

let mergedStats = inputFiles |> Seq.map(File.ReadLines)
                             |> Seq.concat
                             |> createAnalysisStat

File.WriteAllText($"{analyzeOutputDir}/merged.json", JsonSerializer.Serialize(mergedStats, jsonSettings))
writeWordCloud mergedStats.topWords $"{wordCloudOutputDir}/merged.jpg"

File.ReadLines $"{mainDir}/outputs/analyze/mnsz.txt"
|> createAnalysisStat
|> fun k -> writeWordCloud k.topWords $"{wordCloudOutputDir}/mnsz.jpg"

let endTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
printfn "All done in %dms" (endTime - beginTime)