package magyarlancanalyzer.model;

public final class MagyarlancWord {

    public final String word;
    public final String baseForm;
    public final String POSTag;
    public final String properties;

    public MagyarlancWord(String[] outputColumns) {
        word = outputColumns[0];
        baseForm = outputColumns[1];
        POSTag = outputColumns[2];
        properties = outputColumns[3];
    }
}