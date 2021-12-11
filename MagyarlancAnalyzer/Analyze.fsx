open System
open System.IO
open System.Text.Encodings.Web
open System.Text.Json

type MagyarlancInfo = {| Word: string; PartOfSpeech: string |}

let createTopNWordsStat(wordCount: int, stats: seq<MagyarlancInfo>) =
    stats |> Seq.groupBy(fun k -> k.Word)
          |> Seq.map(fun (word, stats) -> (word, {| count = stats |> Seq.length; pos = stats |> Seq.head |> fun k -> k.PartOfSpeech |}))
          |> Seq.sortByDescending(fun (_, k) -> k.count)
          |> Seq.truncate(wordCount)
          |> dict

let createTopWordsStatPerPOS(wordCount: int, stats: seq<MagyarlancInfo>) =
    stats |> Seq.groupBy(fun k -> k.PartOfSpeech)
          |> Seq.map(fun (pos, stats) -> (pos, stats |> Seq.countBy(fun k -> k.Word) |> Seq.sortByDescending(fun (_, frequency) -> frequency) |> Seq.truncate(wordCount) |> dict))
          |> dict

let createAnalysisStat(filePath: string) =
    let fileStats = File.ReadLines(filePath) |> Seq.filter(fun k -> k <> String.Empty)
                                             |> Seq.map(fun k -> k.Split('\t'))
                                             |> Seq.map(fun k -> {| Word = k.[1]; PartOfSpeech = k.[3] |})
    {|
        posCounts = fileStats |> Seq.countBy(fun k -> k.PartOfSpeech) |> dict
        topWords = createTopNWordsStat(5, fileStats)
        topWordsPerPOS = createTopWordsStatPerPOS(5, fileStats)
    |}


let outputOptions = JsonSerializerOptions(WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping)

if Directory.Exists("analyze_outputs") then
    Directory.Delete("analyze_outputs", true)

Directory.CreateDirectory("analyze_outputs")
Directory.GetFiles("magyarlanc_outputs") |> Seq.map(fun k -> (k, JsonSerializer.Serialize(createAnalysisStat(k), outputOptions)))
                                         |> Seq.iter(fun (filePath, stats) -> File.WriteAllText($"analyze_outputs/{Path.GetFileNameWithoutExtension(filePath)}.json", stats))