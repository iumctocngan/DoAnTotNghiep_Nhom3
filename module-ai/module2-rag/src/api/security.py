from dataclasses import dataclass
from typing import Annotated

import jwt
from fastapi import Depends, HTTPException, Request, status
from fastapi.security import HTTPAuthorizationCredentials, HTTPBearer
from jwt import InvalidTokenError

from core import Settings

bearer_scheme = HTTPBearer(auto_error=False)

JWT_ALGORITHM = "HS256"
CORE_ROLE_CLAIM = "role"


@dataclass(frozen=True)
class CurrentUser:
    user_id: str
    role: str


def decode_current_user(token: str, settings: Settings) -> CurrentUser:

    try:
        claims = jwt.decode(
            token,
            settings.jwt_secret,
            algorithms=[JWT_ALGORITHM],
            audience=settings.jwt_audience,
            issuer=settings.jwt_issuer,
            options={"require": ["exp", "iss", "aud"]},
        )
    except InvalidTokenError as error:
        raise HTTPException(
            status_code=status.HTTP_401_UNAUTHORIZED,
            detail="Token truy cập không hợp lệ hoặc đã hết hạn.",
            headers={"WWW-Authenticate": "Bearer"},
        ) from error

    subject = claims.get("sub")
    raw_role = claims.get(CORE_ROLE_CLAIM)
    user_id = subject.strip() if isinstance(subject, str) else ""
    role = raw_role.strip().lower() if isinstance(raw_role, str) else ""
    if not user_id or role not in {"admin", "teacher"}:
        raise HTTPException(
            status_code=status.HTTP_403_FORBIDDEN,
            detail="Tài khoản CMS này không có quyền sử dụng mô-đun RAG.",
        )
    return CurrentUser(user_id=user_id, role=role)


async def get_current_user(
    request: Request,
    credentials: Annotated[
        HTTPAuthorizationCredentials | None, Depends(bearer_scheme)
    ] = None,
) -> CurrentUser:
    if credentials is None:
        raise HTTPException(
            status_code=status.HTTP_401_UNAUTHORIZED,
            detail="Thiếu Bearer access token trong tiêu đề yêu cầu.",
            headers={"WWW-Authenticate": "Bearer"},
        )
    return decode_current_user(credentials.credentials, request.app.state.settings)
