import matplotlib.pyplot as pyplot
from sys import argv
import json

chart_args = json.loads(argv[1])
chart_type = chart_args['chartType']
data = chart_args['data']
ax = pyplot.subplots()[1]

if chart_type == 'pos_counts':
    edges = ax.pie(data.values(), autopct = '%1.0f%%')[0]
    ax.legend(edges, data.keys(), title = chart_args['legendTitle'], loc = 'center left', bbox_to_anchor = (1, 0, 0.5, 1))
elif chart_type == 'per_grade_pos_counts':
    legend_dots = []

    for stats in data.values():
        keys = stats.keys()
        values = stats.values()

        ax.plot(keys, values)
        legend_dots.append(ax.scatter(keys, values))

    box = ax.get_position()
    ax.set_position([box.x0, box.y0, box.width * 0.9, box.height])
    ax.legend(legend_dots, data.keys(), title = chart_args['legendTitle'], loc = 'center left', bbox_to_anchor = (1, 0.5))
    ax.set_xlabel('Évfolyam')
    ax.set_ylabel('Darabszám')
    ax.grid(axis = 'y')

pyplot.savefig(chart_args['filePath'])