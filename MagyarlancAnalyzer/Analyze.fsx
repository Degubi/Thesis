open System
open System.IO
open System.Text.Encodings.Web
open System.Text.Json

type MagyarlancInfo = {| Word: string; PartOfSpeech: string |}

let createTopNWordsStat(wordCount: int, stats: MagyarlancInfo[]) =
    stats |> Seq.groupBy(fun k -> k.Word)
          |> Seq.map(fun (word, stats) -> (word, {| count = stats |> Seq.length; pos = stats |> Seq.head |> fun k -> k.PartOfSpeech |}))
          |> Seq.sortByDescending(fun (_, k) -> k.count)
          |> Seq.truncate(wordCount)

let createTopWordsStatPerPOS(wordCount: int, stats: MagyarlancInfo[]) =
    stats |> Seq.groupBy(fun k -> k.PartOfSpeech)
          |> Seq.map(fun (pos, stats) -> (pos, stats |> Seq.countBy(fun k -> k.Word) |> Seq.sortByDescending(fun (_, frequency) -> frequency) |> Seq.truncate(wordCount) |> dict))

let createAnalysisStat(lines: seq<string>) =
    let fileStats = lines |> Seq.filter(fun k -> k <> String.Empty)
                          |> Seq.map(fun k -> k.Split('\t'))
                          |> Seq.map(fun k -> {| Word = k.[1]; PartOfSpeech = k.[3] |})
                          |> Seq.toArray
    {|
        posCounts = fileStats |> Seq.countBy(fun k -> k.PartOfSpeech) |> dict
        topWords = createTopNWordsStat(5, fileStats) |> dict
        topWordsPerPOS = createTopWordsStatPerPOS(5, fileStats) |> dict
    |}

let jsonSettings = JsonSerializerOptions(WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping)
let inputFiles = Directory.GetFiles("magyarlanc_outputs")

Directory.CreateDirectory("analyze_outputs")

inputFiles |> Seq.map(fun k -> File.ReadLines(k) |> createAnalysisStat |> fun m -> (k, JsonSerializer.Serialize(m, jsonSettings)))
           |> Seq.iter(fun (filePath, stats) -> File.WriteAllText($"analyze_outputs/{Path.GetFileNameWithoutExtension(filePath)}.json", stats))

inputFiles |> Seq.map(File.ReadLines)
           |> Seq.concat
           |> createAnalysisStat
           |> fun k -> File.WriteAllText($"analyze_outputs/merged.json", JsonSerializer.Serialize(k, jsonSettings))