package magyarlancanalyzer;

import hu.u_szeged.magyarlanc.*;
import hu.u_szeged.magyarlanc.util.*;
import java.io.*;
import java.nio.charset.*;
import java.nio.file.*;

public final class Main {

    public static void main(String[] args) throws Exception {
        var outputDirPath = Path.of("../magyarlanc_outputs");
        if(!Files.exists(outputDirPath)) {
            Files.createDirectory(outputDirPath);
        }

        Magyarlanc.fullInit();

        try(var inputFiles = Files.list(Path.of("../paraphrised_extracts"))) {
            inputFiles.forEach(Main::analyzeWithMagyarlanc);
        }
    }

    private static void analyzeWithMagyarlanc(Path inputFile) {
        System.out.println("\nAnalyzing file: " + inputFile);

        try(var output = Files.newBufferedWriter(Path.of("../magyarlanc_outputs/" + inputFile.getFileName()), StandardCharsets.UTF_8, StandardOpenOption.TRUNCATE_EXISTING)) {
            var input = SafeReader.read(inputFile.toString(), StandardCharsets.UTF_8.name());

            Magyarlanc.parse(input, output);
        } catch (IOException e) {
            e.printStackTrace();
        }

        System.out.println("Done with file: " + inputFile);
    }
}