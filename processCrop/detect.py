from fastapi import FastAPI, File, UploadFile, Form, HTTPException, Request, Response
from fastapi.responses import StreamingResponse
from ultralytics import YOLO
from PIL import Image
import io
import os
from typing import Dict, List, Any, Optional
import asyncio
import json
from pydantic import BaseModel # <<--- 이 줄을 추가합니다.

app = FastAPI()

# --- 모델 로드 ---
MODEL_PATH = './runs/detect/train/weights/best.pt' # 실제 모델 경로로 수정하세요.
yolo_model = None
model_load_error = None

# 사용자 ID별 SSE 구독자(Unity 클라이언트) 큐를 저장하는 딕셔너리
user_specific_subscribers: Dict[str, asyncio.Queue] = {}

try:
    if not os.path.exists(MODEL_PATH):
        cwd = os.getcwd()
        raise FileNotFoundError(
            f"YOLO model 'best.pt' not found at the relative path '{MODEL_PATH}' "
            f"from the current working directory '{cwd}'. "
            "Please ensure the server is started from the correct project root directory "
            "and the model path is correct."
        )
    yolo_model = YOLO(MODEL_PATH)
    print(f"YOLO model loaded successfully from '{MODEL_PATH}' (relative to CWD: {os.getcwd()})")
except Exception as e:
    model_load_error = str(e)
    print(f"Error loading YOLO model: {e}")

async def send_detection_update_to_specific_subscriber(user_id: str, data_json_string: str):
    """특정 user_id의 SSE 구독자에게 탐지 결과 업데이트를 전송합니다."""
    if user_id in user_specific_subscribers:
        queue = user_specific_subscribers[user_id]
        try:
            await queue.put(data_json_string)
        except Exception as e:
            print(f"Error sending update to subscriber queue for user_id {user_id}: {e}")
    else:
        pass


@app.post("/detect/")
async def detect_objects_save_and_send_to_user(
    user_id: str = Form(..., description="요청을 보낸 사용자의 고유 ID"),
    x_divisions: int = Form(..., description="이미지를 가로로 나눌 섹터의 수 (열의 수)"),
    y_divisions: int = Form(..., description="이미지를 세로로 나눌 섹터의 수 (행의 수)"),
    image: UploadFile = File(...)
) -> Dict[str, Any]:
    global yolo_model, model_load_error

    if model_load_error or yolo_model is None:
        raise HTTPException(status_code=500, detail=f"YOLO model not loaded. Error: {model_load_error}")

    if not user_id:
        raise HTTPException(status_code=400, detail="Parameter 'user_id' must be provided.")
    if x_divisions <= 0:
        raise HTTPException(status_code=400, detail="Parameter 'x_divisions' must be a positive integer.")
    if y_divisions <= 0:
        raise HTTPException(status_code=400, detail="Parameter 'y_divisions' must be a positive integer.")

    try:
        contents = await image.read()
        pil_image = Image.open(io.BytesIO(contents))
        img_width, img_height = pil_image.size
        if img_width == 0 or img_height == 0:
            raise HTTPException(status_code=400, detail="Uploaded image has zero width or height.")
    except Exception as e:
        raise HTTPException(status_code=400, detail=f"Invalid image file or could not read image: {str(e)}")
    
    try:
        results = yolo_model.predict(pil_image, verbose=False)
        results[0].show()
        sector_width = img_width / x_divisions
        sector_height = img_height / y_divisions
        grid_results: Dict[str, List[Dict[str, Any]]] = {
            f"{row}-{col}": [] for row in range(y_divisions) for col in range(x_divisions)
        }

        for r in results:
            boxes = r.boxes
            for box in boxes:
                cls_id = int(box.cls[0])
                class_name = yolo_model.names[cls_id]
                _coords_tensor = box.xyxy[0]
                _x1, _y1, _x2, _y2 = _coords_tensor[0].item(), _coords_tensor[1].item(), _coords_tensor[2].item(), _coords_tensor[3].item()
                center_x = (_x1 + _x2) / 2
                center_y = (_y1 + _y2) / 2
                
                sector_col = min(int(center_x // sector_width), x_divisions - 1)
                sector_row = min(int(center_y // sector_height), y_divisions - 1)
                sector_key = f"{sector_row}-{sector_col}"

                obj_data = {
                    "sector_row": sector_row,
                    "sector_col": sector_col,
                    "Lv": class_name[:2],
                    "type": class_name[3:] 
                }
                if sector_key in grid_results:
                    grid_results[sector_key].append(obj_data)
        
        json_for_file_storage = json.dumps(grid_results, indent=4, ensure_ascii=False)
        json_for_sse_transmission = json.dumps(grid_results, separators=(',', ':'))

        user_data_dir = os.path.join("./Login/db", user_id) # YOLO 서버 기준 상대 경로
        os.makedirs(user_data_dir, exist_ok=True)
        output_filepath = os.path.join(user_data_dir, "detection_results.json") # 탐지 결과 저장 파일명

        with open(output_filepath, "w", encoding="utf-8") as f:
            f.write(json_for_file_storage)
        print(f"Detection results saved to: {output_filepath}")

        await send_detection_update_to_specific_subscriber(user_id, json_for_sse_transmission)
        
        return {"status": "success", "message": f"Detection processed, results saved to '{output_filepath}', and sent to user {user_id} if subscribed."}

    except Exception as e:
        print(f"Error during detection processing for user_id {user_id}: {e}")
        import traceback
        traceback.print_exc()
        raise HTTPException(status_code=500, detail=f"An error occurred during object detection: {str(e)}")

# --- UserDataResponse 모델 정의 (수정됨) ---
class UserDataResponse(BaseModel):
    id: str
    nickname: Optional[str] = None
    level: Optional[int] = None  # 타입을 int로 명확히 함
    exp: Optional[int] = None    # 경험치 필드 추가
    detection_data: Optional[Dict[str, Any]] = None


# --- /user_data/ 엔드포인트 (수정됨) ---
USER_AUTH_DATA_FILENAME = "user_data.json"       # 회원가입 정보 파일명 (auth 서버와 파일명 일치 가정)
DETECTION_DATA_FILENAME = "detection_results.json" # YOLO 탐지 결과 파일명
DB_BASE_DIR = "./Login/db"                         # 데이터 기본 경로 (YOLO 서버 기준)

@app.post("/user_data/", response_model=UserDataResponse)
async def get_user_data_and_respond(user_id: str = Form(...)):
    """
    지정된 사용자 ID에 대해 저장된 인증 정보(닉네임, 레벨, 경험치)와 탐지 데이터를 조회하여
    HTTP 응답으로 반환합니다. SSE 재전송은 하지 않습니다.
    """
    if not user_id:
        raise HTTPException(status_code=400, detail="Parameter 'user_id' must be provided.")

    user_auth_data_path = os.path.join(DB_BASE_DIR, user_id, USER_AUTH_DATA_FILENAME)
    detection_data_path = os.path.join(DB_BASE_DIR, user_id, DETECTION_DATA_FILENAME)

    nickname_from_file: Optional[str] = None
    level_from_file: Optional[int] = None
    exp_from_file: Optional[int] = None # 경험치 변수 추가
    detection_data_content: Optional[Dict[str, Any]] = None

    # 1. 사용자 인증 정보 파일(닉네임, 레벨, 경험치 등) 읽기
    if os.path.exists(user_auth_data_path):
        try:
            with open(user_auth_data_path, "r", encoding="utf-8") as f:
                auth_data = json.load(f)
            nickname_from_file = auth_data.get("nickname")
            level_from_file = auth_data.get("level")
            exp_from_file = auth_data.get("exp") # 경험치 읽어오기
        except json.JSONDecodeError:
            print(f"Warning: Error decoding {USER_AUTH_DATA_FILENAME} for user_id: {user_id}. File might be corrupted.")
        except Exception as e:
            print(f"Warning: Could not read {USER_AUTH_DATA_FILENAME} for user_id: {user_id}: {e}")
    else:
        print(f"Info: {USER_AUTH_DATA_FILENAME} not found for user_id: {user_id}")

    # 2. 탐지 데이터 파일 읽기
    if os.path.exists(detection_data_path):
        try:
            with open(detection_data_path, "r", encoding="utf-8") as f:
                detection_data_content = json.load(f)
        except json.JSONDecodeError:
            raise HTTPException(status_code=500, detail=f"Error decoding {DETECTION_DATA_FILENAME} for user_id: {user_id}. File might be corrupted.")
        except Exception as e:
            raise HTTPException(status_code=500, detail=f"Error reading {DETECTION_DATA_FILENAME} for user_id: {user_id}: {e}")
    else:
        print(f"Info: {DETECTION_DATA_FILENAME} not found for user_id: {user_id}")

    if not os.path.exists(user_auth_data_path) and not os.path.exists(detection_data_path):
        raise HTTPException(status_code=404, detail=f"No data or user information found for user_id: {user_id}")

    return UserDataResponse(
        id=user_id,
        nickname=nickname_from_file,
        level=level_from_file,
        exp=exp_from_file, # 응답에 경험치 포함
        detection_data=detection_data_content
    )

# --- /detection_stream/{user_id} 엔드포인트 ---
@app.get("/detection_stream/{user_id}")
async def detection_event_stream_for_user(user_id: str, request: Request):
    if not user_id:
        raise HTTPException(status_code=400, detail="User ID must be provided in the path.")

    client_queue = asyncio.Queue()
    user_specific_subscribers[user_id] = client_queue
    print(f"New SSE client connected for user_id: {user_id}. Client: {request.client}. Total specific subscribers: {len(user_specific_subscribers)}")

    async def event_generator():
        processed_events_count = 0
        try:
            while True:
                if await request.is_disconnected():
                    print(f"SSE client for user_id {user_id} ({request.client}) disconnected. Processed {processed_events_count} events for this client.")
                    break
                
                message = await client_queue.get()
                if message is None: 
                    print(f"Received None, breaking event generator for user_id {user_id} ({request.client}). Processed {processed_events_count} events.")
                    break
                
                yield f"data: {message}\n\n"
                processed_events_count += 1
        except asyncio.CancelledError:
            print(f"SSE client for user_id {user_id} ({request.client}) connection cancelled. Processed {processed_events_count} events.")
        except Exception as e:
            print(f"Exception in event_generator for user_id {user_id} ({request.client}): {e}. Processed {processed_events_count} events.")
        finally:
            if user_id in user_specific_subscribers and user_specific_subscribers[user_id] == client_queue:
                del user_specific_subscribers[user_id]
            print(f"SSE client for user_id {user_id} ({request.client}) cleanup. Total specific subscribers: {len(user_specific_subscribers)}. This client processed {processed_events_count} events.")

    return StreamingResponse(event_generator(), media_type="text/event-stream")


if __name__ == "__main__":
    import uvicorn
    # ./Login/db 폴더가 없으면 생성 (YOLO 서버 기준)
    # 이 경로는 회원가입/로그인 서버의 DB_DIR과 일치하거나, 접근 가능해야 합니다.
    if not os.path.exists(DB_BASE_DIR):
        os.makedirs(DB_BASE_DIR, exist_ok=True)
        print(f"Created base directory {DB_BASE_DIR}")
        
    uvicorn.run(app, host="0.0.0.0", port=8000, log_level="info")