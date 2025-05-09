import os
import json
import bcrypt
import aiofiles # 비동기 파일 처리를 위해 추가
from typing import Optional, Dict, Any, List # List 추가

from fastapi import FastAPI, Form, HTTPException, status, Depends
from pydantic import BaseModel

# --- Configuration ---
DB_DIR = "db"  # 데이터를 저장할 최상위 디렉토리 이름
if not os.path.exists(DB_DIR):
    os.makedirs(DB_DIR)


# --- Pydantic Models ---
class UserBase(BaseModel):
    id: str
    nickname: str
    level: int
    exp: int

class UserCreate(UserBase):
    password: str

class UserLogin(BaseModel):
    id: str
    password: str

class UserInDB(UserBase):
    hashed_password: str

class UserResponse(UserBase): # 응답 시에는 UserBase의 필드만 사용
    pass

# --- Password Utilities ---
def get_password_hash(password: str) -> str:
    pwd_bytes = password.encode('utf-8')
    salt = bcrypt.gensalt()
    hashed_password = bcrypt.hashpw(pwd_bytes, salt)
    return hashed_password.decode('utf-8')

def verify_password(plain_password: str, hashed_password: str) -> bool:
    password_byte_enc = plain_password.encode('utf-8')
    hashed_password_byte_enc = hashed_password.encode('utf-8')
    return bcrypt.checkpw(password_byte_enc, hashed_password_byte_enc)

# --- FastAPI Application ---
app = FastAPI(title="사용자 인증 및 정보 관리 API",
              description="회원가입, 로그인, 사용자 정보 조회, 업데이트 및 전체 사용자 목록 기능을 제공하는 API")

# --- Helper functions for file paths ---
def sanitize_user_id_for_path(user_id: str) -> str:
    safe_user_id = "".join(c for c in user_id if c.isalnum() or c in ('-', '_', '.'))
    if not safe_user_id:
        raise ValueError("ID가 파일 경로로 사용하기에 적합하지 않습니다. (필터링 후 빈 문자열)")
    return safe_user_id

def get_user_directory_path(user_id: str) -> str:
    safe_user_id = sanitize_user_id_for_path(user_id)
    return os.path.join(DB_DIR, safe_user_id)

def get_user_data_file_path(user_id: str) -> str:
    user_dir = get_user_directory_path(user_id)
    return os.path.join(user_dir, "user_data.json")

# --- API Endpoints ---

@app.post("/register/", response_model=UserResponse, status_code=status.HTTP_201_CREATED,
          summary="회원가입 (레벨 및 경험치 포함)")
async def register_user(
    id: str = Form(...),
    password: str = Form(...),
    nickname: str = Form(...),
    level: int = Form(1, description="사용자 초기 레벨 (기본값: 1)"),
    exp: int = Form(0, description="사용자 초기 경험치 (기본값: 0)")
):
    try:
        user_dir_path = get_user_directory_path(id)
        user_data_file_path = get_user_data_file_path(id)
    except ValueError as e:
        raise HTTPException(status_code=status.HTTP_400_BAD_REQUEST, detail=str(e))

    if os.path.exists(user_dir_path):
        raise HTTPException(
            status_code=status.HTTP_400_BAD_REQUEST,
            detail="이미 사용 중인 ID입니다."
        )

    hashed_password = get_password_hash(password)
    user_data_to_store = UserInDB(
        id=id,
        nickname=nickname,
        level=level,
        exp=exp,
        hashed_password=hashed_password
    )

    try:
        os.makedirs(user_dir_path, exist_ok=True)
        async with aiofiles.open(user_data_file_path, "w", encoding="utf-8") as f:
            await f.write(json.dumps(user_data_to_store.model_dump(), ensure_ascii=False, indent=4))
    except Exception as e:
        if os.path.exists(user_data_file_path):
            try: await aiofiles.os.remove(user_data_file_path)
            except: pass
        if os.path.exists(user_dir_path) and not os.listdir(user_dir_path):
            try: os.rmdir(user_dir_path)
            except: pass
        raise HTTPException(
            status_code=status.HTTP_500_INTERNAL_SERVER_ERROR,
            detail=f"사용자 정보 저장 중 오류 발생: {str(e)}"
        )
    return UserResponse(**user_data_to_store.model_dump())


@app.post("/login/", response_model=UserResponse, summary="로그인 (레벨 및 경험치 포함)")
async def login_for_access_token(
    id: str = Form(...),
    password: str = Form(...)
):
    try:
        user_data_file_path = get_user_data_file_path(id)
    except ValueError as e:
        raise HTTPException(status_code=status.HTTP_400_BAD_REQUEST, detail=str(e))

    if not os.path.exists(user_data_file_path):
        raise HTTPException(
            status_code=status.HTTP_401_UNAUTHORIZED,
            detail="ID 또는 비밀번호가 잘못되었습니다.",
            headers={"WWW-Authenticate": "Bearer"},
        )

    try:
        async with aiofiles.open(user_data_file_path, "r", encoding="utf-8") as f:
            content = await f.read()
            stored_user_data_dict = json.loads(content)
        user_in_db = UserInDB(**stored_user_data_dict)
    except Exception as e:
        print(f"Error during login data processing for {id}: {e}")
        raise HTTPException(
            status_code=status.HTTP_500_INTERNAL_SERVER_ERROR,
            detail="사용자 정보 처리 중 오류 발생."
        )

    if not verify_password(password, user_in_db.hashed_password):
        raise HTTPException(
            status_code=status.HTTP_401_UNAUTHORIZED,
            detail="ID 또는 비밀번호가 잘못되었습니다.",
            headers={"WWW-Authenticate": "Bearer"},
        )
    return UserResponse(**user_in_db.model_dump())

@app.get("/users/me/", response_model=UserResponse, summary="현재 사용자 정보 조회 (예시, 레벨 및 경험치 포함)")
async def read_users_me(current_user_id: str = Form(..., description="정보를 조회할 사용자의 ID")): # Form으로 변경하여 /docs에서 테스트 용이
    try:
        user_data_file_path = get_user_data_file_path(current_user_id)
    except ValueError as e:
        raise HTTPException(status_code=status.HTTP_400_BAD_REQUEST, detail=str(e))

    if not os.path.exists(user_data_file_path):
        raise HTTPException(status_code=status.HTTP_404_NOT_FOUND, detail="사용자를 찾을 수 없습니다.")

    try:
        async with aiofiles.open(user_data_file_path, "r", encoding="utf-8") as f:
            content = await f.read()
            stored_user_data_dict = json.loads(content)
        user_in_db = UserInDB(**stored_user_data_dict)
    except Exception as e:
        raise HTTPException(
            status_code=status.HTTP_500_INTERNAL_SERVER_ERROR,
            detail=f"사용자 정보 조회 중 오류 발생: {str(e)}"
        )
    return UserResponse(**user_in_db.model_dump())


@app.post("/users/update/", response_model=UserResponse, summary="사용자 정보 업데이트 (닉네임, 레벨, 경험치)")
async def update_user_info(
    id: str = Form(...),
    nickname: Optional[str] = Form(None, description="새로운 닉네임 (변경 원치 않으면 비워둠)"),
    level: Optional[int] = Form(None, description="새로운 레벨 (변경 원치 않으면 비워둠)"),
    exp: Optional[int] = Form(None, description="새로운 경험치 (변경 원치 않으면 비워둠)")
):
    try:
        user_data_file_path = get_user_data_file_path(id)
    except ValueError as e:
        raise HTTPException(status_code=status.HTTP_400_BAD_REQUEST, detail=str(e))

    if not os.path.exists(user_data_file_path):
        raise HTTPException(status_code=status.HTTP_404_NOT_FOUND, detail=f"사용자 ID '{id}'를 찾을 수 없습니다.")

    try:
        async with aiofiles.open(user_data_file_path, "r", encoding="utf-8") as f:
            content = await f.read()
            stored_user_data_dict = json.loads(content)
        user_in_db = UserInDB(**stored_user_data_dict)

        updated_fields = False
        if nickname is not None:
            user_in_db.nickname = nickname
            updated_fields = True
        if level is not None:
            user_in_db.level = level
            updated_fields = True
        if exp is not None:
            user_in_db.exp = exp
            updated_fields = True
        
        if not updated_fields:
            return UserResponse(**user_in_db.model_dump())

        async with aiofiles.open(user_data_file_path, "w", encoding="utf-8") as f:
            await f.write(json.dumps(user_in_db.model_dump(), ensure_ascii=False, indent=4))

    except json.JSONDecodeError:
        raise HTTPException(status_code=status.HTTP_500_INTERNAL_SERVER_ERROR, detail="사용자 데이터 파일 형식이 잘못되었습니다.")
    except Exception as e:
        print(f"Error during user update for {id}: {e}")
        raise HTTPException(status_code=status.HTTP_500_INTERNAL_SERVER_ERROR, detail=f"사용자 정보 업데이트 중 오류 발생: {str(e)}")

    return UserResponse(**user_in_db.model_dump())

# --- 모든 사용자 정보 조회 엔드포인트 추가 ---
@app.get("/users/all/", response_model=List[UserResponse], summary="모든 사용자 정보 목록 조회")
async def read_all_users():
    """
    DB에 저장된 모든 사용자의 정보(ID, 닉네임, 레벨, 경험치) 목록을 반환합니다.
    """
    all_users_data = []
    if not os.path.exists(DB_DIR) or not os.path.isdir(DB_DIR):
        print(f"Warning: Database directory '{DB_DIR}' not found.")
        return all_users_data

    try:
        # os.listdir은 동기 함수이지만, 사용자 수가 매우 많지 않다면 일반적으로 허용 가능합니다.
        # 매우 많은 사용자를 예상한다면 anyio.to_thread.run_sync 등을 고려할 수 있습니다.
        user_id_folders = [folder_name for folder_name in os.listdir(DB_DIR) if os.path.isdir(os.path.join(DB_DIR, folder_name))]
    except Exception as e:
        print(f"Error listing user directories in '{DB_DIR}': {e}")
        raise HTTPException(status_code=status.HTTP_500_INTERNAL_SERVER_ERROR, detail="사용자 목록을 불러오는 중 오류가 발생했습니다.")

    for user_id_from_folder in user_id_folders:
        try:
            # 폴더명이 실제 사용자 ID라고 가정하고 파일 경로 생성
            # sanitize_user_id_for_path는 ID에 특수문자가 있을 경우를 대비하지만,
            # 여기서는 폴더명을 기준으로 하므로 직접 경로를 조합합니다.
            # 만약 폴더명이 sanitize된 ID라면 get_user_data_file_path(user_id_from_folder) 사용도 가능합니다.
            user_data_file = os.path.join(DB_DIR, user_id_from_folder, "user_data.json")

            if os.path.exists(user_data_file):
                async with aiofiles.open(user_data_file, "r", encoding="utf-8") as f:
                    content = await f.read()
                    user_data_dict = json.loads(content)
                
                # UserResponse 모델에 맞게 데이터 추출 (비밀번호 제외)
                # UserInDB(**user_data_dict)를 사용하여 Pydantic 검증 후 UserResponse로 변환 가능
                user_obj = UserInDB(**user_data_dict)
                all_users_data.append(UserResponse(**user_obj.model_dump()))
            else:
                print(f"Warning: user_data.json not found in directory '{user_id_from_folder}'. Skipping.")
        except json.JSONDecodeError:
            print(f"Warning: Corrupted user_data.json for user_id directory '{user_id_from_folder}'. Skipping.")
        except Exception as e: # Pydantic ValidationError 등 포함
            print(f"Error processing data for user_id directory '{user_id_from_folder}': {e}. Skipping.")
            
    return all_users_data

# --- Uvicorn main entry point ---
if __name__ == "__main__":
    import uvicorn
    print(f"FastAPI Auth 서버를 시작합니다 (데이터 저장 폴더: '{DB_DIR}/<ID>/user_data.json' 구조). http://127.0.0.1:8001/docs 에서 API 문서를 확인하세요.")
    uvicorn.run(app, host="0.0.0.0", port=8001)