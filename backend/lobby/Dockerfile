# Use official light-weight Python image from Docker Hub
FROM python:3.11-slim

# Set working directory inside the container
WORKDIR /app

# Copy dependency list and install them
COPY requirements.txt .
RUN pip install --no-cache-dir -r requirements.txt

# Copy the rest of the lobby source code
COPY . .

# Expose the port that Lobby Manager listens on (FastAPI default)
EXPOSE 8000

# Run the FastAPI app so the container stays alive and serves HTTP requests.
CMD ["uvicorn", "main:app", "--host", "0.0.0.0", "--port", "8000"]
