#r "nuget: WordCloud"

open System
open System.Collections.Generic
open System.IO
open System.Text.Encodings.Web
open System.Text.Json
open WordCloud

let ignoredPOSes = [| "PUNCT" |]

let createAnalysisStat(lines: seq<string>) =
    let fileStats = lines |> Seq.filter(fun k -> k <> String.Empty)
                          |> Seq.map(fun k -> k.Split('\t'))
                          |> Seq.map(fun k -> {| word = k.[1]; pos = k.[3] |})
                          |> Seq.toArray

    let topWords = fileStats |> Seq.filter(fun k -> not(Array.Exists(ignoredPOSes, fun m -> m = k.pos)))
                             |> Seq.groupBy(fun k -> k.word)
                             |> Seq.map(fun (word, stats) -> (word, {| count = stats |> Seq.length; pos = stats |> Seq.head |> fun k -> k.pos |}))
                             |> Seq.sortByDescending(fun (_, k) -> k.count)
                             |> Seq.truncate(50)

    let topWordsPerPOS = fileStats |> Seq.groupBy(fun k -> k.pos)
                                   |> Seq.map(fun (pos, stats) -> (pos, stats |> Seq.countBy(fun k -> k.word) |> Seq.sortByDescending(fun (_, frequency) -> frequency) |> Seq.truncate(5) |> dict))
    {|
        posCounts = fileStats |> Seq.countBy(fun k -> k.pos) |> dict
        topWords = topWords|> dict
        topWordsPerPOS = topWordsPerPOS |> dict
    |}

let writeWordCloud(topWords: IDictionary<string, {| count: int; pos: string |}>, fileName: string) =
    WordCloud(1024, 768, true).Draw(topWords.Keys |> List, topWords |> Seq.map(fun k -> k.Value.count) |> List).Save($"word_clouds/{fileName}")


let jsonSettings = JsonSerializerOptions(WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping)
let inputFiles = Directory.GetFiles("magyarlanc_outputs")

Directory.CreateDirectory("analyze_outputs")
Directory.CreateDirectory("word_clouds")

let perBookStats = inputFiles |> Seq.map(fun k -> (Path.GetFileNameWithoutExtension(k), k |> File.ReadLines |> createAnalysisStat))
                              |> Seq.toArray

perBookStats |> Seq.iter(fun (fileName, stats) -> File.WriteAllText($"analyze_outputs/{fileName}.json", JsonSerializer.Serialize(stats, jsonSettings)))
perBookStats |> Seq.iter(fun (fileName, stats) -> writeWordCloud(stats.topWords, $"{fileName}.jpg"))

let mergedStats = inputFiles |> Seq.map(File.ReadLines)
                             |> Seq.concat
                             |> createAnalysisStat

File.WriteAllText("analyze_outputs/merged.json", JsonSerializer.Serialize(mergedStats, jsonSettings))
writeWordCloud(mergedStats.topWords, "merged.jpg")