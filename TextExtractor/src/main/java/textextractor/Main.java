package textextractor;

import java.io.*;
import java.nio.file.*;
import java.util.*;
import java.util.stream.*;
import javax.imageio.*;
import net.sourceforge.tess4j.*;
import org.apache.pdfbox.pdmodel.*;
import org.apache.pdfbox.rendering.*;

public final class Main {
    private static final boolean IMAGE_DEBUG = false;

    public static void main(String[] args) throws Exception {
        var params = IntStream.iterate(0, k -> k < args.length, k -> k + 2)
                              .boxed()
                              .collect(Collectors.toMap(k -> args[k], k -> args[k + 1]));

        if(params.isEmpty()) {
            System.out.println("Options: -input {pdfPath} -pageRange {n-m}");
            return;
        }

        var inputPath = Path.of(params.get("-input"));
        var inputPDFPaths = Files.isDirectory(inputPath) ? listPDFsInDirectory(inputPath) : new Path[] { inputPath };
        var optionalPageRange = params.get("-pageRange");
        var splitPageRange = optionalPageRange != null ? optionalPageRange.split("-") : null;

        System.out.println("Extracting " + inputPDFPaths.length + " pdf files");

        var totalStartTime = System.currentTimeMillis();
        for(var inputPDFPath : inputPDFPaths) {
            try(var inputPDFDocument = PDDocument.load(inputPDFPath.toFile())) {
                var imageRenderer = new PDFRenderer(inputPDFDocument);
                var tesseract = ThreadLocal.withInitial(Main::createTesseract);
                var startingPage = splitPageRange == null ? 1 : Integer.parseInt(splitPageRange[0]) - 1;
                var endingPage = splitPageRange == null ? inputPDFDocument.getNumberOfPages() - 1 : Integer.parseInt(splitPageRange[1]) - 1;

                System.out.println("Extracting text from: " + inputPDFPath + ", page range: " + startingPage + "-" + endingPage);

                var pdfStartTime = System.currentTimeMillis();

                var rawExtractedText = IntStream.rangeClosed(startingPage, endingPage)
                                                .parallel()
                                                .mapToObj(i -> extractTextFromPage(imageRenderer, tesseract, i))
                                                .sorted(Comparator.comparingInt(ExtractResult::page))
                                                .map(ExtractResult::content)
                                                .collect(Collectors.joining());

                var perParagraphExtract = Arrays.stream(rawExtractedText.split("\n\n"))
                                                .map(k -> k.replace("-\n", "").replace('\n', ' '))
                                                .collect(Collectors.joining("\n"));

                var pdfEndTime = System.currentTimeMillis();

                var inputPathString = inputPDFPath.toString();
                var outputDirectory = "./extract/" + inputPathString.substring(0, inputPathString.lastIndexOf('.'));

                Files.createDirectories(Path.of(outputDirectory));
                Files.writeString(Path.of(outputDirectory + "/raw.txt"), rawExtractedText);
                Files.writeString(Path.of(outputDirectory + "/paragraph.txt"), perParagraphExtract);

                System.out.println("PDF done in " + (pdfEndTime - pdfStartTime) + "ms!");
            }
        }

        var totalEndTime = System.currentTimeMillis();
        System.out.println("All done in " + (totalEndTime - totalStartTime) + "ms!");
    }


    private static Path[] listPDFsInDirectory(Path inputPath) {
        try(var folder = Files.walk(inputPath)) {
            return folder.filter(k -> Files.isRegularFile(k))
                         .filter(k -> k.toString().endsWith(".pdf"))
                         .toArray(Path[]::new);
        } catch (IOException e) {
            e.printStackTrace();
            return new Path[0];
        }
    }

    private static ExtractResult extractTextFromPage(PDFRenderer imageRenderer, ThreadLocal<Tesseract> tesseract, int page) {
        try {
            var image = imageRenderer.renderImageWithDPI(page, 300);
            var croppedImage = image.getSubimage(150, 150, image.getWidth() - 300, image.getHeight() - 300);

            if(IMAGE_DEBUG) {
                ImageIO.write(croppedImage, "png", new File("image-extract-" + page + ".png"));
            }

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