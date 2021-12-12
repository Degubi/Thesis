open System
open System.IO

let paraphriseFileContent filePath =
    File.ReadAllText(filePath) |> fun k -> k.Split("\n\n")
                               |> Seq.map(fun k -> k.Replace("-\n", "").Replace("\n", " "))
                               |> fun k -> String.Join("\n", k)

Directory.CreateDirectory("paraphrised_extracts")
Directory.GetFiles("text_extracts") |> Seq.map(fun k -> (k, paraphriseFileContent(k)))
                                    |> Seq.iter(fun (path, content) -> File.WriteAllText($"paraphrised_extracts/{Path.GetFileName(path)}", content))