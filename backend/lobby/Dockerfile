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

# Command to run your Python script
CMD ["python", "main.py"]