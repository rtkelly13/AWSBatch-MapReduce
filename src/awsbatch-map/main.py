import datashader as ds, pandas as pd, colorcet as cc, numpy as np
from datashader.utils import export_image
from pyproj import Transformer

TRAN_4326_TO_3857 = Transformer.from_crs("EPSG:4326", "EPSG:3857")

exportPath = "../../data/results"
run_normal = True
run_clean = True

def fix_num(v):
    return np.float32(v)

def apply_transform(x):
    x, y = TRAN_4326_TO_3857.transform(x['dropoff_latitude'], x['dropoff_longitude'])
    return pd.Series([fix_num(x), fix_num(y)], index=['dropoff_x', 'dropoff_y'])

def run():
    if run_normal:
        path = '../../data/yellow_tripdata_2010-01.parquet'
        df = pd.read_parquet(path, columns=['dropoff_longitude', 'dropoff_latitude'])

        print(len(df.index))
        dropoff_x, dropoff_y = TRAN_4326_TO_3857.transform(
            df['dropoff_latitude'].to_numpy(),
            df['dropoff_longitude'].to_numpy())
        df['dropoff_x'] = dropoff_x
        df['dropoff_y'] = dropoff_y
        #df = df.apply(apply_transform, axis=1)
        filt = (df['dropoff_x'] >= -8254332.0) & (df['dropoff_x'] <= -8209813.5) & \
               (4965255.5 <= df['dropoff_y']) & (4988769.5 >= df['dropoff_y'])
        df = df.loc[filt]
        print(df.dropoff_x.mean())
        print(df.dropoff_y.mean())
        print(len(df.index))
        print(df.head(100))
        print(df.dtypes)

        normal_parquet_path = exportPath + "/normal.parquet"
        df.to_parquet(path=normal_parquet_path, engine='fastparquet')
        canvas = ds.Canvas(plot_width=1200, plot_height=1000)
        agg = canvas.points(df, 'dropoff_x', 'dropoff_y')
        shaded = ds.tf.shade(agg, cmap=cc.fire)
        # img = ds.tf.set_background(shaded, "black")
        export_image(shaded, "output-original", background="black", export_path=exportPath)

    if run_clean:
        path = '../../data/nyc_taxi_wide.parquet'
        df = pd.read_parquet(path, columns=['dropoff_x', 'dropoff_y'])
        print(df.dropoff_x.mean())
        print(df.dropoff_y.mean())
        print(df.head(100))
        print(df.dtypes)

        print(df['dropoff_x'].min())
        print(df['dropoff_x'].max())
        print(df['dropoff_y'].min())
        print(df['dropoff_y'].max())
        df.to_parquet(path=exportPath + "/clean.parquet", engine='fastparquet')

        canvas = ds.Canvas(plot_width=1200, plot_height=1000)
        agg = canvas.points(df, 'dropoff_x', 'dropoff_y')
        shaded = ds.tf.shade(agg, cmap=cc.fire)
        # img = ds.tf.set_background(shaded, "black")
        export_image(shaded, "output", background="black", export_path=exportPath)


run()

