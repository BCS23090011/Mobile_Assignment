from flask import Flask, render_template, redirect, url_for, request, flash
import requests
import uuid # 新增：用于生成 GUID
from datetime import datetime# 新增：用于生成时间
import re 

app = Flask(__name__)
app.secret_key = "secret_key_for_flash_messages"

# ✅ 你的 Firebase URL
FIREBASE_URL = "https://mobile-44ff2-default-rtdb.firebaseio.com/"
AUTH_PARAM = "" 

# --- 🟢 新增：发送通知的通用函数 ---
def send_notification(user_id, title, body, related_market_id, msg_type):
    """
    向指定用户发送通知，数据结构对应 C# 的 NotificationMessage 类
    """
    if not user_id:
        print("Error: No User ID provided for notification")
        return

    notification_data = {
        "Id": str(uuid.uuid4()),            # 对应 C# Guid.NewGuid()
        "UserId": user_id,
        "Title": title,
        "Body": body,
        "Type": msg_type,                   # Approval, Rejection, General
        "RelatedMarketId": related_market_id,
        "CreatedAt": datetime.utcnow().isoformat(), # 对应 C# DateTime
        "IsRead": False
    }

    # 策略：将通知直接推送到 notifications/{userId} 下
    # 这样你的 C# GetNotificationsAsync(userId) 只需要读取这个节点即可
    url = f"{FIREBASE_URL}notifications/{user_id}.json{AUTH_PARAM}"
    requests.post(url, json=notification_data)

@app.route('/send_broadcast', methods=['POST'])
def send_broadcast():
    message = request.form.get('message')
    
    if message:
        # 构造广播数据
        # 注意：UserId 我们设为 "ALL"，Type 设为 "Broadcast"
        broadcast_data = {
            "Id": str(uuid.uuid4()),
            "UserId": "ALL", 
            "Title": "📢 Announcement",
            "Body": message,
            "Type": "Broadcast", 
            "CreatedAt": datetime.utcnow().isoformat(),
            "IsRead": False 
        }
        
        # 写入到 notifications/broadcast 节点
        url = f"{FIREBASE_URL}notifications/broadcast.json{AUTH_PARAM}"
        requests.post(url, json=broadcast_data)
        
        flash(f"✅ Broadcast sent: '{message}'", "success")
    
    return redirect(url_for('index'))

@app.route('/')
def index():
    # ... (保持原有的 index 代码不变) ...
    # 1. 获取所有数据
    markets_res = requests.get(f"{FIREBASE_URL}markets.json{AUTH_PARAM}")
    submissions_res = requests.get(f"{FIREBASE_URL}submissions.json{AUTH_PARAM}")
    
    pending_items = []
    
    # 2. 处理“新店申请”
    if markets_res.status_code == 200 and markets_res.json():
        for key, item in markets_res.json().items():
            if item.get('Status') == 'Pending':
                item['Id'] = key
                item['IsDeleteRequest'] = False
                pending_items.append(item)

    # 3. 处理“删除申请”
    if submissions_res.status_code == 200 and submissions_res.json():
        for key, item in submissions_res.json().items():
            if item.get('Status') == 'Pending' and item.get('RequestType') == 'Delete':
                item['Id'] = key
                item['IsDeleteRequest'] = True
                item['Name'] = f"❌ DELETE: {item.get('MarketName')}"
                
                change_details = item.get('ChangeDetails', '')

                # 🔥 关键修复：使用正则表达式提取 URL
                # 匹配 [Photo: 后面的完整 URL (https://...token=xxx)
                url_match = re.search(r'\[Photo:\s*(https?://[^\s\]]+)\]', change_details)
                
                if url_match:
                    # 1. 提取匹配到的 URL，并赋给 PhotoUrl 供 HTML 使用
                    item['PhotoUrl'] = url_match.group(1) 
                    
                    # 2. 将 URL 从 Description 中移除，保持描述的干净
                    item['Description'] = change_details.replace(url_match.group(0), "").strip()
                else:
                    item['PhotoUrl'] = "" 
                    item['Description'] = change_details # 描述为原始文本
                
                pending_items.append(item)
    
    return render_template('index.html', markets=pending_items)
    pass


# --- 针对“新店”的操作 ---

@app.route('/approve_new/<market_id>')
def approve_new(market_id):
    # 1. 获取市场信息以便知道是谁提交的 (SubmittedBy)
    market_url = f"{FIREBASE_URL}markets/{market_id}.json{AUTH_PARAM}"
    market_data = requests.get(market_url).json()
    
    # 2. 更新状态
    requests.patch(market_url, json={"Status": "Approved"})
    
    # 3. ✅ 发送通知
    if market_data:
        user_id = market_data.get('SubmittedBy') # 确保你的 Market 数据里有这个字段
        market_name = market_data.get('Name', 'Market')
        send_notification(
            user_id=user_id,
            title="Market Approved! 🎉",
            body=f"Good news! Your submission for '{market_name}' has been approved and is now visible.",
            related_market_id=market_id,
            msg_type="Approval"
        )

    return redirect(url_for('index'))

@app.route('/reject_new/<market_id>')
def reject_new(market_id):
    # 1. 获取市场信息
    market_url = f"{FIREBASE_URL}markets/{market_id}.json{AUTH_PARAM}"
    market_data = requests.get(market_url).json()
    
    # 2. 更新状态
    requests.patch(market_url, json={"Status": "Rejected"})
    
    # 3. ✅ 发送通知
    if market_data:
        user_id = market_data.get('SubmittedBy')
        market_name = market_data.get('Name', 'Market')
        send_notification(
            user_id=user_id,
            title="Submission Rejected",
            body=f"We reviewed your submission for '{market_name}' but could not approve it at this time.",
            related_market_id=market_id,
            msg_type="Rejection"
        )

    return redirect(url_for('index'))

# --- 针对“删除申请”的操作 ---

@app.route('/confirm_delete/<submission_id>') 
# 注意：这里传进来的应该是 submission 的 ID (因为是处理 submission 表)
def confirm_delete(submission_id):
    # 1. 获取申请单信息 (为了拿 UserId 和 MarketId)
    sub_url = f"{FIREBASE_URL}submissions/{submission_id}.json{AUTH_PARAM}"
    sub_data = requests.get(sub_url).json()
    
    if sub_data:
        market_id = sub_data.get('MarketId') # 假设 submission 里存了 MarketId
        user_id = sub_data.get('SubmittedBy')
        market_name = sub_data.get('MarketName')

        # 2. 执行删除逻辑 (你的原有逻辑)
        # 将 Market 设为 Rejected (下架)
        if market_id:
            requests.patch(f"{FIREBASE_URL}markets/{market_id}.json{AUTH_PARAM}", json={"Status": "Rejected"})
        
        # 将 Submission 设为 Approved (申请通过)
        requests.patch(sub_url, json={"Status": "Approved"})

        # 3. ✅ 发送通知
        send_notification(
            user_id=user_id,
            title="Deletion Request Approved",
            body=f"Your request to delete '{market_name}' has been processed.",
            related_market_id=market_id,
            msg_type="Approval"
        )

    return redirect(url_for('index'))

@app.route('/reject_delete/<submission_id>')
def reject_delete(submission_id):
    # 1. 获取申请单信息
    sub_url = f"{FIREBASE_URL}submissions/{submission_id}.json{AUTH_PARAM}"
    sub_data = requests.get(sub_url).json()
    
    if sub_data:
        user_id = sub_data.get('SubmittedBy')
        market_name = sub_data.get('MarketName')
        
        # 2. 拒绝申请
        requests.patch(sub_url, json={"Status": "Rejected"})
        
        # 3. ✅ 发送通知
        send_notification(
            user_id=user_id,
            title="Deletion Request Denied",
            body=f"Your request to delete '{market_name}' was not approved. The market remains listed.",
            related_market_id=sub_data.get('MarketId'),
            msg_type="Rejection"
        )

    return redirect(url_for('index'))

if __name__ == '__main__':
    app.run(debug=True)