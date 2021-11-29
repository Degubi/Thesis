open System
open System.IO

let paraphrise filePath =
    File.ReadAllText(filePath) |> fun k -> k.Split("\n\n")
                               |> Seq.map(fun k -> k.Replace("-\n", "").Replace("\n", " "))
                               |> fun k -> String.Join("\n", k)

Directory.Delete("paraphrised_extracts", true)
Directory.CreateDirectory("paraphrised_extracts")
Directory.GetFiles("text_extracts") |> Seq.iter(fun k -> File.WriteAllText("paraphrised_extracts/" + Path.GetFileName(k), paraphrise(k)))