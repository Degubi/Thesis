package textextractor;

import java.io.*;
import java.nio.file.*;
import java.util.*;
import java.util.stream.*;
import net.sourceforge.tess4j.*;
import org.apache.pdfbox.pdmodel.*;
import org.apache.pdfbox.rendering.*;

public final class Main {

    public static void main(String[] args) throws Exception {
        var outputDir = Path.of("../outputs/text/raw");

        if(!Files.exists(outputDir)) {
            Files.createDirectory(outputDir);
        }

        var inputPDFPaths = listPDFs();
        var tesseract = ThreadLocal.withInitial(Main::createTesseract);
        var totalStartTime = System.currentTimeMillis();

        System.out.println("Extracting " + inputPDFPaths.length + " pdf files");

        for(var inputPDFPath : inputPDFPaths) {
            try(var inputPDFDocument = PDDocument.load(inputPDFPath.toFile())) {
                System.out.println("Extracting text from: " + inputPDFPath);

                var imageRenderer = new PDFRenderer(inputPDFDocument);
                var pdfStartTime = System.currentTimeMillis();

                var rawExtractedText = IntStream.rangeClosed(1, inputPDFDocument.getNumberOfPages() - 1)
                                                .parallel()
                                                .mapToObj(i -> extractTextFromPage(imageRenderer, tesseract, i))
                                                .sorted(Comparator.comparingInt(ExtractResult::page))
                                                .map(ExtractResult::content)
                                                .collect(Collectors.joining());

                var pdfEndTime = System.currentTimeMillis();
                var inputFileName = inputPDFPath.getFileName().toString();

                Files.writeString(Path.of(outputDir + inputFileName.substring(0, inputFileName.lastIndexOf('.')) + ".txt"), rawExtractedText);
                System.out.println("PDF done in " + (pdfEndTime - pdfStartTime) + "ms!\n");
            }
        }

        var totalEndTime = System.currentTimeMillis();
        System.out.println("All done in " + (totalEndTime - totalStartTime) + "ms!");
    }


    private static Path[] listPDFs() {
        try(var folder = Files.list(Path.of("../pdfs"))) {
            return folder.toArray(Path[]::new);
        } catch (IOException e) {
            e.printStackTrace();
            return new Path[0];
        }
    }

    private static ExtractResult extractTextFromPage(PDFRenderer imageRenderer, ThreadLocal<Tesseract> tesseract, int page) {
        System.out.println("Extracting page: " + page);

        try {
            var image = imageRenderer.renderImageWithDPI(page, 300);
            var croppedImage = image.getSubimage(150, 150, image.getWidth() - 300, image.getHeight() - 300);

            return new ExtractResult(tesseract.get().doOCR(croppedImage), page);
        } catch (IOException | TesseractException e) {
            e.printStackTrace();
            return new ExtractResult("", 0);
        }
    }

    private static Tesseract createTesseract() {
        var tesseract = new Tesseract();
        tesseract.setDatapath(Path.of("tessdata").toAbsolutePath().toString());
        tesseract.setTessVariable("user_defined_dpi", "300");
        tesseract.setPageSegMode(1);
        tesseract.setLanguage("hun");
        return tesseract;
    }

    record ExtractResult(String content, int page) {}
}