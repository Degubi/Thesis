package textextractor;

import java.nio.file.*;
import net.sourceforge.tess4j.*;
import org.apache.pdfbox.pdmodel.*;
import org.apache.pdfbox.rendering.*;

public final class Main {

    public static void main(String[] args) throws Exception {
        var areArgsMissing = args.length == 0;
        var inputPDFPath = Path.of(areArgsMissing ? "pdfs/9/szgy.pdf" : args[0]);
        var extractionOutputPath = Path.of(areArgsMissing ? "extract.txt" : args[1]);

        System.out.println("Extracting text from: " + inputPDFPath);

        try(var inputPDFDocument = PDDocument.load(inputPDFPath.toFile())) {
            var image = new PDFRenderer(inputPDFDocument).renderImageWithDPI(11, 300);
            var tesseract = new Tesseract();
            tesseract.setDatapath(Path.of("tessdata").toAbsolutePath().toString());
            tesseract.setTessVariable("user_defined_dpi", "300");
            tesseract.setPageSegMode(1);
            tesseract.setLanguage("hun");

            var extractedText = tesseract.doOCR(image);

            System.out.println("Writing result to: " + extractionOutputPath);

            Files.writeString(extractionOutputPath, extractedText);

            System.out.println("Done!");
        }
    }
}