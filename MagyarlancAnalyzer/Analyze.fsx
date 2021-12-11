open System
open System.IO
open System.Text.Json

let createAnalysisStat filePath =
    let stats = File.ReadLines(filePath) |> Seq.filter(fun k -> k <> String.Empty)
                                         |> Seq.map(fun k -> k.Split('\t'))
                                         |> Seq.map(fun k -> {| Word = k.[1]; PartOfSpeech = k.[3] |})
    {|
        POSCounts = stats |> Seq.countBy(fun k -> k.PartOfSpeech) |> dict
    |}

let outputOptions = JsonSerializerOptions(WriteIndented = true)

if Directory.Exists("analyze_outputs") then
    Directory.Delete("analyze_outputs", true)

Directory.CreateDirectory("analyze_outputs")
Directory.GetFiles("magyarlanc_outputs") |> Seq.map(fun k -> (k, JsonSerializer.Serialize(createAnalysisStat(k), outputOptions)))
                                         |> Seq.iter(fun (filePath, stats) -> File.WriteAllText($"analyze_outputs/{Path.GetFileNameWithoutExtension(filePath)}.json", stats))