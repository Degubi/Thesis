open System
open System.IO

let paraphriseFileContent filePath =
    File.ReadAllText(filePath) |> fun k -> k.Split("\n\n")
                               |> Seq.map(fun k -> k.Replace("-\n", "").Replace("\n", " "))
                               |> fun k -> String.Join("\n", k)

let mainDir = Directory.GetParent(__SOURCE_DIRECTORY__).FullName
let inputDir = $"{mainDir}/outputs/text/raw"
let outputDir = $"{mainDir}/outputs/text/paraphrised"

Directory.CreateDirectory(outputDir)
Directory.GetFiles(inputDir) |> Seq.map(fun k -> (k, paraphriseFileContent(k)))
                             |> Seq.iter(fun (path, content) -> File.WriteAllText($"{outputDir}/{Path.GetFileName(path)}", content))