open System
open System.IO

if Directory.Exists("paraphrised_extracts") then
    Directory.Delete("paraphrised_extracts", true)

Directory.CreateDirectory("paraphrised_extracts")
Directory.GetFiles("text_extracts") |> Seq.iter(fun filePath ->
    File.ReadAllText(filePath) |> fun k -> k.Split("\n\n")
                               |> Seq.map(fun k -> k.Replace("-\n", "").Replace("\n", " "))
                               |> fun k -> String.Join("\n", k)
                               |> fun k -> File.WriteAllText($"paraphrised_extracts/{Path.GetFileName(filePath)}", k)
)