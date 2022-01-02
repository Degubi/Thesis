package analyzers;

import static java.nio.file.StandardOpenOption.*;

import hu.u_szeged.magyarlanc.*;
import hu.u_szeged.magyarlanc.util.*;
import java.io.*;
import java.nio.charset.*;
import java.nio.file.*;

public final class MagyarlancRunner {

    public static void main(String[] args) throws IOException {
        var outputDirPath = Path.of("../outputs/analyze/magyarlanc");
        if(!Files.exists(outputDirPath)) {
            Files.createDirectory(outputDirPath);
        }

        Magyarlanc.morphInit();

        try(var inputFiles = Files.list(Path.of("../outputs/text/paraphrised"))) {
            inputFiles.forEach(k -> analyzeWithMagyarlanc(k, outputDirPath));
        }
    }

    private static void analyzeWithMagyarlanc(Path inputFile, Path outputDirPath) {
        System.out.println("\nAnalyzing file: " + inputFile);

        try(var output = Files.newBufferedWriter(Path.of(outputDirPath.toString() + inputFile.getFileName()), StandardCharsets.UTF_8, TRUNCATE_EXISTING, CREATE)) {
            var input = SafeReader.read(inputFile.toString(), StandardCharsets.UTF_8.name());

            Magyarlanc.morphParse(input, output);
        } catch (IOException e) {
            e.printStackTrace();
        }

        System.out.println("Done with file: " + inputFile);
    }
}