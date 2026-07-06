# Use official light-weight Python image from Docker Hub
FROM python:3.11-slim

# Set working directory inside the container
WORKDIR /app

# Copy dependency list and install them
COPY requirements.txt .
RUN pip install --no-cache-dir -r requirements.txt

# Copy the game room logic code
COPY . .

# Expose the internal port for game room communication (WebSocket)
EXPOSE 5000

# Command to run the world room API
CMD ["uvicorn", "room:app", "--host", "0.0.0.0", "--port", "5000"]
