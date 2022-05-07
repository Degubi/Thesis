open System
open System.IO

let paraphriseFileContent file =
    file |> File.ReadAllText
         |> fun k -> k.Replace(Environment.NewLine, "\n")
         |> fun k -> k.Split "\n\n"
         |> Seq.map(fun k -> k.Replace("-\n", "").Replace("\n", " "))
         |> fun k -> String.Join("\n", k)

let mainDir = Directory.GetParent(__SOURCE_DIRECTORY__).FullName
let inputDir = $"{mainDir}/outputs/text/raw"
let outputDir = $"{mainDir}/outputs/text/paraphrised"
let beginTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()

Directory.CreateDirectory outputDir
Directory.GetFiles inputDir |> Seq.iter(fun k -> File.WriteAllText($"{outputDir}/{Path.GetFileName k}", paraphriseFileContent k))

let endTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
printfn "All done in %dms" (endTime - beginTime)