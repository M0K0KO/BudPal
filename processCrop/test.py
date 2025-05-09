import requests
import json
import time
import os # 폴더 내 이미지 파일을 가져오기 위해 추가

# --- 설정 변수 ---
SERVER_URL = "http://localhost:8000/detect/"

USER_ID_TO_SEND = "defaultUser"  # <<--- 서버로 전송할 사용자 ID
IMAGE_FOLDER_PATH = f'./Login/db/{USER_ID_TO_SEND}/'  # 이미지가 있는 폴더 경로
GRID_X = 5  # 가로 분할 수
GRID_Y = 4  # 세로 분할 수
SEND_INTERVAL_SECONDS = 5  # 각 이미지 전송 사이의 시간 간격 (초)
SUPPORTED_IMAGE_EXTENSIONS = ('.png', '.jpg', '.jpeg', '.bmp', '.gif') # 지원하는 이미지 확장자

def get_image_paths(folder_path):
    """지정된 폴더에서 이미지 파일 경로 목록을 가져옵니다."""
    image_paths = []
    if not os.path.isdir(folder_path):
        print(f"오류: 폴더를 찾을 수 없습니다: {folder_path}")
        return image_paths

    for filename in os.listdir(folder_path):
        if filename.lower().endswith(SUPPORTED_IMAGE_EXTENSIONS):
            image_paths.append(os.path.join(folder_path, filename))
    if not image_paths:
        print(f"알림: {folder_path} 에서 이미지를 찾을 수 없습니다.")
    return image_paths

def send_image_request(image_path, url, user_id, grid_x, grid_y): # user_id 인자 추가
    """지정된 이미지에 대해 서버에 요청을 보내고 응답을 처리합니다."""
    try:
        with open(image_path, 'rb') as img_file:
            files = {'image': (os.path.basename(image_path), img_file, 'image/jpeg')} # 파일 이름만 추출
            # user_id를 data에 포함
            data = {'user_id': user_id, 'x_divisions': grid_x, 'y_divisions': grid_y}

            print(f"\n[{time.strftime('%Y-%m-%d %H:%M:%S')}] 서버로 요청 전송 중:")
            print(f"  URL: {url}")
            print(f"  이미지: '{image_path}'")
            print(f"  user_id: {user_id}") # user_id 로그 추가
            print(f"  x_divisions: {grid_x}")
            print(f"  y_divisions: {grid_y}")

            response = requests.post(url, files=files, data=data, timeout=30) # 타임아웃 추가 (초)
            response.raise_for_status()  # HTTP 오류 발생 시 예외 발생
            result_json = response.json()

            print("\n--- 서버 응답 (JSON) ---")
            print(json.dumps(result_json, indent=2, ensure_ascii=False))
            return True # 성공적으로 전송 및 응답 받음

    except FileNotFoundError:
        print(f"오류: 이미지 파일을 찾을 수 없습니다: {image_path}")
    except requests.exceptions.HTTPError as errh:
        print(f"HTTP 오류: {errh}")
        try:
            # 응답 내용을 확인하여 더 자세한 오류 정보 얻기
            print(f"오류 세부 정보 (서버): {response.json()}")
        except requests.exceptions.JSONDecodeError: # json 디코딩 실패 시 text로 출력
            print(f"오류 세부 정보 (서버): {response.text}")
    except requests.exceptions.ConnectionError as errc:
        print(f"연결 오류: {errc} (서버가 {url} 에서 실행 중인지 확인하세요.)")
    except requests.exceptions.Timeout as errt:
        print(f"시간 초과 오류: {errt} (서버 응답이 너무 오래 걸립니다.)")
    except requests.exceptions.RequestException as err:
        print(f"요청 중 알 수 없는 오류 발생: {err}")
    except Exception as e:
        print(f"처리 중 예기치 않은 오류 발생: {e}")
    return False # 전송 또는 처리 실패

def main():
    """메인 실행 함수"""
    image_paths_to_send = get_image_paths(IMAGE_FOLDER_PATH)

    if not image_paths_to_send:
        print("전송할 이미지가 없습니다. 프로그램을 종료합니다.")
        return

    print(f"총 {len(image_paths_to_send)}개의 이미지를 찾았습니다. 사용자 ID '{USER_ID_TO_SEND}'로 {SEND_INTERVAL_SECONDS}초 간격으로 전송을 시작합니다.")
    print("---")

    try:
        for image_path in image_paths_to_send:
            # send_image_request 호출 시 USER_ID_TO_SEND 전달
            success = send_image_request(image_path, SERVER_URL, USER_ID_TO_SEND, GRID_X, GRID_Y)
            if success:
                print(f"'{os.path.basename(image_path)}' 처리 완료.")
            else:
                print(f"'{os.path.basename(image_path)}' 처리 중 오류 발생.")

            # 마지막 이미지가 아닌 경우에만 대기
            if image_path != image_paths_to_send[-1]:
                print(f"\n다음 이미지 전송까지 {SEND_INTERVAL_SECONDS}초 대기 중...")
                time.sleep(SEND_INTERVAL_SECONDS)
            print("---")

        print("\n모든 이미지 처리가 완료되었습니다.")

    except KeyboardInterrupt:
        print("\n사용자에 의해 프로그램이 중단되었습니다.")
    finally:
        print("프로그램을 종료합니다.")

if __name__ == "__main__":
    main()