"""
Kết nối SQL Server dùng pyodbc + SQLAlchemy.
Module 3 chỉ READ từ Shared DB — không write trực tiếp (để đảm bảo integrity).
Ghi kết quả dự đoán vào StudentRenewalPredictions qua service riêng.
"""
from sqlalchemy import create_engine, text
from sqlalchemy.orm import sessionmaker, DeclarativeBase

from ai_service.config import settings


def _build_sqlalchemy_url() -> str:
    """Chuyển pyodbc connection string sang SQLAlchemy URL."""
    import urllib
    encoded = urllib.parse.quote_plus(settings.db_connection_string)
    return f"mssql+pyodbc:///?odbc_connect={encoded}"


engine = create_engine(
    _build_sqlalchemy_url(),
    pool_pre_ping=True,       # kiểm tra kết nối trước khi dùng
    pool_size=5,
    max_overflow=10,
    echo=settings.debug,
)

SessionLocal = sessionmaker(bind=engine, autocommit=False, autoflush=False)


class Base(DeclarativeBase):
    pass


def get_db():
    """FastAPI dependency: cung cấp DB session và tự đóng sau khi dùng."""
    db = SessionLocal()
    try:
        yield db
    finally:
        db.close()


def ping_db() -> bool:
    """Kiểm tra kết nối database. Dùng trong health check."""
    try:
        with engine.connect() as conn:
            conn.execute(text("SELECT 1"))
        return True
    except Exception:
        return False
