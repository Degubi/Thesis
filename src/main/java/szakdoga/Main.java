package szakdoga;

import java.nio.file.*;
import java.util.*;
import net.sourceforge.tess4j.*;
import org.apache.pdfbox.pdmodel.*;
import org.apache.pdfbox.rendering.*;
import szakdoga.model.*;

public final class Main {

    public static void main(String[] args) throws Exception {
        var sentences = analyzePDFText("pdfs/9/szgy.pdf");

    }

    public static MagyarlancSentence[] analyzePDFText(String pdfPath) throws Exception {
        var extractionOutputFile = "tempOut.txt";
        var extractionOutputPath = Path.of(extractionOutputFile);

        System.out.println("Extracting text from: " + pdfPath);

        var extractedText = extractTextFromPDF(pdfPath);

        Files.writeString(extractionOutputPath, extractedText);

        System.out.println("Analyzing text from: " + pdfPath);

        var magyarlancOutputFile = "analyse.txt";
        var magyarlancOutputPath = Path.of(magyarlancOutputFile);

        Runtime.getRuntime().exec("java -jar magyarlanc.jar -mode morphparse -input " + extractionOutputFile + " -output " + magyarlancOutputFile).waitFor();

        System.out.println("Done with: " + pdfPath);

        var magyarlancOutput = Files.readString(magyarlancOutputPath);

        Files.delete(extractionOutputPath);
        Files.delete(magyarlancOutputPath);

        return Arrays.stream(magyarlancOutput.split("\n\n"))
                     .map(MagyarlancSentence::new)
                     .toArray(MagyarlancSentence[]::new);
    }


    private static String extractTextFromPDF(String pdfPath) throws Exception {
        try(var inputPDFDocument = PDDocument.load(Path.of(pdfPath).toFile())) {
            var image = new PDFRenderer(inputPDFDocument).renderImageWithDPI(11, 300);
            var tesseract = new Tesseract();
            tesseract.setDatapath(Path.of("tessdata").toAbsolutePath().toString());
            tesseract.setTessVariable("user_defined_dpi", "300");
            tesseract.setPageSegMode(1);
            tesseract.setLanguage("hun");

            return tesseract.doOCR(image);
        }
    }
}