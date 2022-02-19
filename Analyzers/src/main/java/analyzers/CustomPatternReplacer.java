package analyzers;

import java.io.Serializable;
import java.util.regex.Matcher;
import java.util.regex.Pattern;

/**
 * Original file: splitter.utils.PatternReplacer
 */
public class CustomPatternReplacer implements Serializable {

    protected String sourcePattern;
    protected Matcher sourcePatternMatcher;
    protected String replacementPattern;

    public CustomPatternReplacer(String sourcePattern, String replacementPattern) {
        this.sourcePattern = sourcePattern;
        this.replacementPattern = replacementPattern;

        this.sourcePatternMatcher = Pattern.compile(sourcePattern).matcher("");
    }

    public String[] matchGroups(String s) {
        String[] result = null;

        if (sourcePatternMatcher.reset(s).find()) {
            int groupCount = sourcePatternMatcher.groupCount();

            result = new String[groupCount + 1];

            for (int i = 0; i <= groupCount; i++) {
                result[i] = sourcePatternMatcher.group(i);
            }
        }

        return result;
    }

    public String replace(String s) {
        return sourcePatternMatcher.pattern().matcher(s).replaceAll(replacementPattern);
    }

    public Matcher getMatcher() {
        return sourcePatternMatcher;
    }

    @Override
    public String toString() {
        return sourcePattern + " -> " + replacementPattern;
    }
}