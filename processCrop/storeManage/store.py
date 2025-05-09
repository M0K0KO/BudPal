from fastapi import FastAPI, HTTPException, Body
from pydantic import BaseModel, Field
from pathlib import Path
import json
import os
from datetime import datetime, timezone
from typing import List, Optional
import uuid # log_id 생성을 위해 추가

# FastAPI 앱 인스턴스 생성
app = FastAPI(title="Inventory Management API", version="1.0.0")

# 데이터베이스 디렉터리 설정 및 생성
DB_DIR = Path("db")
DB_DIR.mkdir(parents=True, exist_ok=True)

# --- Pydantic 모델 정의 ---

class ItemTransaction(BaseModel):
    id: str = Field(..., description="거래를 요청하는 사용자의 ID") # 구매/일반판매 시 사용자 ID
    item_name: str = Field(..., description="물품명", min_length=1)
    count: int = Field(..., description="물품 개수 (일반 구매/판매 시)", gt=0) # 일반 구매/판매용

class PurchaseLogEntry(BaseModel):
    log_id: str = Field(default_factory=lambda: str(uuid.uuid4())) # 각 구매 기록의 고유 ID
    user_id: str # 이 기록을 생성한 구매자 ID
    purchased_count: int
    timestamp: datetime = Field(default_factory=lambda: datetime.now(timezone.utc))

class ItemStock(BaseModel):
    item_name: str
    current_stock: int
    purchase_history: List[PurchaseLogEntry] = []

class SellSpecificRecordPayload(BaseModel):
    item_name: str = Field(..., description="물품명")
    log_id: str = Field(..., description="삭제(판매)할 특정 구매 기록의 log_id")
    # selling_user_id: Optional[str] = None # 이 판매를 수행하는 사용자 ID (감사/권한용, 현재 로직엔 미사용)

# --- 공통 파일 처리 함수 ---
def _update_stock(
    item_name: str,
    change_count: Optional[int], # 구매 시: 구매량, 판매 시: 판매량 (일반 판매 경우)
    operation_type: str,
    transaction_user_id: Optional[str] = None
) -> ItemStock:
    file_path = DB_DIR / f"{item_name}.json"
    current_data_dict = {
        "item_name": item_name,
        "current_stock": 0,
        "purchase_history": []
    }

    if file_path.exists():
        try:
            with open(file_path, 'r', encoding='utf-8') as f:
                content = f.read()
                if content:
                    loaded_data = json.loads(content)
                    current_data_dict["item_name"] = loaded_data.get("item_name", item_name)
                    current_data_dict["current_stock"] = loaded_data.get("current_stock", 0)
                    
                    raw_history = loaded_data.get("purchase_history", [])
                    validated_history_dicts = []
                    for entry_dict in raw_history:
                        try:
                            log_model = PurchaseLogEntry(**entry_dict)
                            validated_history_dicts.append(log_model.model_dump(mode='json'))
                        except Exception as e_load_parse:
                            print(f"Warning: {file_path} 파일 로드 중 유효하지 않은 구매 기록({entry_dict})을 건너<0xEB>니다. 오류: {e_load_parse}")
                    current_data_dict["purchase_history"] = validated_history_dicts
        except json.JSONDecodeError:
            print(f"Warning: {file_path} 파일이 잘못된 JSON 형식이므로 초기화합니다.")
        except Exception as e:
            raise HTTPException(status_code=500, detail=f"파일 읽기 오류 ({file_path}): {e}")

    if operation_type == "purchase":
        if change_count is None or change_count <= 0:
            raise HTTPException(status_code=400, detail="구매 수량은 0보다 커야 합니다.")
        purchase_quantity = change_count
        current_data_dict["current_stock"] += purchase_quantity
        if transaction_user_id:
            new_log_entry = PurchaseLogEntry(
                user_id=transaction_user_id,
                purchased_count=purchase_quantity
            )
            current_data_dict["purchase_history"].append(new_log_entry.model_dump(mode='json'))
            
            temp_log_models = [PurchaseLogEntry(**log_dict) for log_dict in current_data_dict["purchase_history"]]
            temp_log_models.sort(key=lambda x: x.timestamp)
            current_data_dict["purchase_history"] = [log.model_dump(mode='json') for log in temp_log_models]

    elif operation_type == "sell": # 일반 판매 (수량 기준)
        if change_count is None or change_count <= 0:
            raise HTTPException(status_code=400, detail="판매 수량은 0보다 커야 합니다.")
        sell_quantity = change_count
        if current_data_dict["current_stock"] < sell_quantity:
            raise HTTPException(status_code=400, detail=f"'{item_name}'의 재고({current_data_dict['current_stock']}개)가 부족하여 {sell_quantity}개를 판매할 수 없습니다.")
        current_data_dict["current_stock"] -= sell_quantity
    else:
        raise ValueError("잘못된 operation_type입니다.")

    try:
        final_item_stock_model = ItemStock(**current_data_dict)
    except Exception as e_pydantic:
        raise HTTPException(status_code=500, detail=f"데이터 모델 생성 오류: {e_pydantic}")

    try:
        with open(file_path, 'w', encoding='utf-8') as f:
            json.dump(final_item_stock_model.model_dump(mode='json'), f, ensure_ascii=False, indent=4)
    except Exception as e:
        raise HTTPException(status_code=500, detail=f"파일 쓰기 오류 ({file_path}): {e}")

    return final_item_stock_model

# --- API 엔드포인트 ---

@app.post("/purchase", response_model=ItemStock, summary="물품 구매 (일반)", description="ID, 물품명, 개수를 입력받아 재고를 증가시키고, 구매자 ID, 시간, 고유 log_id와 함께 구매 기록을 남깁니다.")
async def purchase_item(transaction: ItemTransaction = Body(...)):
    print(f"구매 요청: 사용자 ID({transaction.id}), 물품명({transaction.item_name}), 개수({transaction.count})")
    if transaction.count is None:
         raise HTTPException(status_code=400, detail="구매 시 'count'는 필수입니다.")
    updated_stock = _update_stock(
        item_name=transaction.item_name,
        change_count=transaction.count,
        operation_type="purchase",
        transaction_user_id=transaction.id
    )
    return updated_stock

@app.post("/sell", response_model=ItemStock, summary="물품 판매 (일반, 수량 기준)", description="ID, 물품명, 개수를 입력받아 전체 재고에서 수량만큼 감소시킵니다. 구매 기록은 직접 변경되지 않습니다.")
async def sell_item(transaction: ItemTransaction = Body(...)):
    print(f"일반 판매 요청: 사용자 ID({transaction.id}), 물품명({transaction.item_name}), 개수({transaction.count})")
    if transaction.count is None:
         raise HTTPException(status_code=400, detail="판매 시 'count'는 필수입니다.")
    file_path = DB_DIR / f"{transaction.item_name}.json"
    if not file_path.exists():
        raise HTTPException(status_code=404, detail=f"'{transaction.item_name}' 물품을 찾을 수 없습니다.")

    updated_stock = _update_stock(
        item_name=transaction.item_name,
        change_count=transaction.count,
        operation_type="sell",
        transaction_user_id=transaction.id # 감사용으로 전달 가능, 현재 로직에선 미사용
    )
    return updated_stock

@app.post("/sell_specific_record", response_model=ItemStock, summary="특정 구매 기록 판매/삭제", description="물품명과 특정 구매 기록의 log_id를 받아 해당 기록을 삭제하고 재고를 조정합니다.")
async def sell_specific_purchase_record(payload: SellSpecificRecordPayload = Body(...)):
    item_name = payload.item_name
    log_id_to_remove = payload.log_id
    print(f"특정 구매 기록 판매/삭제 요청: 물품명({item_name}), Log ID({log_id_to_remove})")

    file_path = DB_DIR / f"{item_name}.json"
    if not file_path.exists():
        raise HTTPException(status_code=404, detail=f"'{item_name}' 물품 정보를 찾을 수 없습니다.")

    current_data_dict = {
        "item_name": item_name,
        "current_stock": 0,
        "purchase_history": []
    }
    try:
        with open(file_path, 'r', encoding='utf-8') as f:
            content = f.read()
            if content:
                loaded_data = json.loads(content)
                current_data_dict["item_name"] = loaded_data.get("item_name", item_name)
                # current_stock은 나중에 재계산하므로 여기서 로드한 값은 참고용
                current_data_dict["current_stock"] = loaded_data.get("current_stock", 0) 
                
                raw_history = loaded_data.get("purchase_history", [])
                validated_history_dicts = []
                for entry_dict in raw_history:
                    try:
                        log_model = PurchaseLogEntry(**entry_dict)
                        validated_history_dicts.append(log_model.model_dump(mode='json'))
                    except Exception as e_load_parse:
                        print(f"Warning: {file_path} 파일 로드 중 유효하지 않은 구매 기록({entry_dict})을 건너<0xEB>니다. 오류: {e_load_parse}")
                current_data_dict["purchase_history"] = validated_history_dicts
    except Exception as e:
        raise HTTPException(status_code=500, detail=f"파일 읽기 오류 ({file_path}): {e}")

    history_list_of_dicts = current_data_dict["purchase_history"]
    log_found = False
    updated_history_list_of_dicts = []

    for log_dict in history_list_of_dicts:
        # Pydantic 모델은 기본값으로 log_id를 생성하므로, 로드된 dict에 log_id가 있는지 직접 확인
        if log_dict.get("log_id") == log_id_to_remove:
            log_found = True
            # 이 기록은 삭제되므로 updated_history_list_of_dicts에 추가하지 않음
            print(f"Log ID '{log_id_to_remove}'를 가진 구매 기록을 찾았습니다. 삭제합니다: {log_dict}")
        else:
            updated_history_list_of_dicts.append(log_dict)
    
    if not log_found:
        raise HTTPException(status_code=404, detail=f"구매 기록 ID '{log_id_to_remove}'를 '{item_name}' 물품에서 찾을 수 없습니다.")

    current_data_dict["purchase_history"] = updated_history_list_of_dicts

    # 재고 일관성을 위해 남은 구매 기록을 바탕으로 current_stock 재계산
    new_current_stock = 0
    final_valid_history_for_stock_calc = []
    for log_d in current_data_dict["purchase_history"]:
        try:
            log_m = PurchaseLogEntry(**log_d) # 유효성 검사 및 객체화
            new_current_stock += log_m.purchased_count
            final_valid_history_for_stock_calc.append(log_m.model_dump(mode='json')) # 다시 dict로
        except Exception as e_recalc:
            print(f"Warning: 재고 재계산 중 유효하지 않은 구매 기록({log_d}) 발견. 오류: {e_recalc}")
    current_data_dict["purchase_history"] = final_valid_history_for_stock_calc # 파싱 성공한 것들만 최종 저장
    current_data_dict["current_stock"] = new_current_stock
    
    try:
        final_item_stock_model = ItemStock(**current_data_dict)
    except Exception as e_pydantic:
        raise HTTPException(status_code=500, detail=f"데이터 모델 생성 오류: {e_pydantic}")
    
    try:
        with open(file_path, 'w', encoding='utf-8') as f:
            json.dump(final_item_stock_model.model_dump(mode='json'), f, ensure_ascii=False, indent=4)
    except Exception as e:
        raise HTTPException(status_code=500, detail=f"파일 쓰기 오류 ({file_path}): {e}")

    return final_item_stock_model


@app.get("/stock/{item_name}", response_model=ItemStock, summary="특정 물품 재고 확인")
async def get_item_stock(item_name: str):
    file_path = DB_DIR / f"{item_name}.json"
    if not file_path.exists():
        raise HTTPException(status_code=404, detail=f"'{item_name}' 물품 정보를 찾을 수 없습니다.")
    try:
        with open(file_path, 'r', encoding='utf-8') as f:
            content = f.read()
            if not content:
                return ItemStock(item_name=item_name, current_stock=0, purchase_history=[]) 
            stock_data_dict = json.loads(content)
            return ItemStock(**stock_data_dict)
    except json.JSONDecodeError:
        raise HTTPException(status_code=500, detail=f"'{item_name}' 물품 파일 JSON 오류")
    except Exception as e:
        raise HTTPException(status_code=500, detail=f"'{item_name}' 물품 데이터 처리 오류: {e}")

@app.get("/stocks", response_model=List[ItemStock], summary="모든 물품 재고 목록 확인")
async def get_all_stocks():
    all_stocks_models: List[ItemStock] = []
    for file_path in DB_DIR.glob("*.json"):
        item_name_from_file = file_path.stem
        try:
            with open(file_path, 'r', encoding='utf-8') as f:
                content = f.read()
                stock_data_dict = {"item_name": item_name_from_file, "current_stock": 0, "purchase_history": []}
                if content:
                    loaded_dict = json.loads(content)
                    stock_data_dict.update(loaded_dict)
                    if stock_data_dict.get("item_name") != item_name_from_file:
                        stock_data_dict["item_name"] = item_name_from_file
                all_stocks_models.append(ItemStock(**stock_data_dict))
        except Exception as e:
            print(f"Warning: {file_path} 처리 오류 ({e}), 목록에서 제외.")
    return all_stocks_models

if __name__ == "__main__":
    import uvicorn
    uvicorn.run(app, host="0.0.0.0", port=8002)