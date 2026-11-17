from dataclasses import dataclass
from typing import Annotated

import jwt
from fastapi import Depends, HTTPException, status
from fastapi.security import HTTPAuthorizationCredentials, HTTPBearer
from jwt import PyJWTError

from ai_service.config import settings


bearer_scheme = HTTPBearer(auto_error=False)


@dataclass(frozen=True)
class CurrentUser:
    user_id: str
    role: str


async def get_current_user(
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

    if (
        not settings.jwt_issuer.strip()
        or not settings.jwt_audience.strip()
        or len(settings.jwt_signing_key.encode("utf-8")) < 32
    ):
        raise HTTPException(
            status_code=status.HTTP_503_SERVICE_UNAVAILABLE,
            detail="AI Service chưa được cấu hình xác thực JWT.",
        )

    try:
        claims = jwt.decode(
            credentials.credentials,
            settings.jwt_signing_key,
            algorithms=["HS256"],
            audience=settings.jwt_audience,
            issuer=settings.jwt_issuer,
            options={"require": ["exp", "iss", "aud", "sub", "role"]},
        )
    except PyJWTError as error:
        raise HTTPException(
            status_code=status.HTTP_401_UNAUTHORIZED,
            detail="Token truy cập không hợp lệ hoặc đã hết hạn.",
            headers={"WWW-Authenticate": "Bearer"},
        ) from error

    user_id = claims.get("sub")
    role = claims.get("role")
    if not isinstance(user_id, str) or not user_id.strip():
        raise HTTPException(
            status_code=status.HTTP_401_UNAUTHORIZED,
            detail="Token không có định danh người dùng hợp lệ.",
            headers={"WWW-Authenticate": "Bearer"},
        )
    if not isinstance(role, str) or not role.strip():
        raise HTTPException(
            status_code=status.HTTP_403_FORBIDDEN,
            detail="Tài khoản không có vai trò hợp lệ.",
        )

    return CurrentUser(user_id=user_id.strip(), role=role.strip().casefold())


def require_roles(*roles: str):
    allowed_roles = frozenset(role.casefold() for role in roles)

    async def dependency(
        user: CurrentUser = Depends(get_current_user),
    ) -> CurrentUser:
        if user.role not in allowed_roles:
            raise HTTPException(
                status_code=status.HTTP_403_FORBIDDEN,
                detail="Tài khoản không có quyền sử dụng chức năng này.",
            )
        return user

    return dependency
