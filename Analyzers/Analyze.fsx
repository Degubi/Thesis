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

let writeWordCloud(topWords: IDictionary<string, {| count: int; pos: string |}>) (posEs: string[]) (filePath: string) =
    let topWordsToWrite = if posEs.Length = 0 then topWords else topWords |> Seq.filter(fun k -> Array.contains k.Value.pos posEs) |> Seq.map(fun k -> (k.Key, k.Value)) |> dict

    WordCloud(1024, 768, true).Draw(topWordsToWrite.Keys |> List, topWordsToWrite |> Seq.map(fun k -> k.Value.count) |> List).Save(filePath)


let mainDir = Directory.GetParent(__SOURCE_DIRECTORY__).FullName
let jsonSettings = JsonSerializerOptions(WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping)
let inputFiles = Directory.GetFiles $"{mainDir}/outputs/analyze/magyarlanc"
let analyzeOutputDir = $"{mainDir}/outputs/stats/magyarlanc"
let wordCloudOutputDir = $"{mainDir}/outputs/word_cloud/magyarlanc"
let customWordcloudPOSes = [| "NOUN"; "VERB"; "ADJ"; "ADP"; "PROPN"; "V"; "N"; "A"; "NU" |]
let beginTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()

Directory.CreateDirectory analyzeOutputDir
Directory.CreateDirectory wordCloudOutputDir

inputFiles |> PSeq.map(fun k -> (Path.GetFileNameWithoutExtension k, k |> File.ReadLines |> createAnalysisStat))
           |> PSeq.iter(fun (fileName, stats) ->
                File.WriteAllText($"{analyzeOutputDir}/{fileName}.json", JsonSerializer.Serialize(stats, jsonSettings))
                writeWordCloud stats.topWords Array.empty $"{wordCloudOutputDir}/{fileName}_all.jpg"
                writeWordCloud stats.topWords customWordcloudPOSes $"{wordCloudOutputDir}/{fileName}_filtered.jpg"
           )

let mergedStats = inputFiles |> Seq.map(File.ReadLines)
                             |> Seq.concat
                             |> createAnalysisStat

File.WriteAllText($"{analyzeOutputDir}/merged.json", JsonSerializer.Serialize(mergedStats, jsonSettings))
writeWordCloud mergedStats.topWords Array.empty $"{wordCloudOutputDir}/merged_all.jpg"
writeWordCloud mergedStats.topWords customWordcloudPOSes $"{wordCloudOutputDir}/merged_filtered.jpg"

let mnszTopWords = File.ReadLines $"{mainDir}/outputs/analyze/mnsz.txt" |> Seq.map(fun k -> k.Split '\t')
                                                                        |> Seq.map(fun k -> (k.[0], {| count = int k.[2]; pos = k.[1] |}))
                                                                        |> dict

writeWordCloud mnszTopWords Array.empty $"{wordCloudOutputDir}/mnsz_all.jpg"
writeWordCloud mnszTopWords customWordcloudPOSes $"{wordCloudOutputDir}/mnsz_filtered.jpg"

let endTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
printfn "All done in %dms" (endTime - beginTime)