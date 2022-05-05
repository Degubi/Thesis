import matplotlib.pyplot as pyplot
from sys import argv
import json

chart_args = json.loads(argv[1])

ax = pyplot.subplots()[1]
edges = ax.pie(chart_args['values'], autopct = '%1.0f%%')[0]
ax.legend(edges, chart_args['labels'], title = chart_args['title'], loc = 'center left', bbox_to_anchor = (1, 0, 0.5, 1))

pyplot.savefig(chart_args['filePath'])