from ultralytics import YOLO

model = YOLO('yolo11s.pt') 

# 모델 학습
results = model.train(data='./box_detection/data.yaml', epochs=50, imgsz=640)

